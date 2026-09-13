using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Viewport;

namespace FlyerFlipper.Core.Store;

/// <inheritdoc cref="IImageStore{TImage}"/>
/// <remarks>
/// Decoding runs on the thread pool; every state change happens on the owning thread, which must have a
/// single-threaded <see cref="SynchronizationContext"/> (the UI thread) so async continuations return to it.
/// </remarks>
public sealed class ImageStore<TImage> : IImageStore<TImage>, IDisposable
    where TImage : class
{
    private readonly IImageCatalog _catalog;
    private readonly IViewportModeService _viewport;
    private readonly IImageLoader _loader;
    private readonly IThumbnailService _thumbnails;
    private readonly IDisplayImageFactory<TImage> _factory;
    private readonly ImageStoreOptions _options;

    private Session _session;
    private bool _disposed;

    public ImageStore(
        IImageCatalog catalog,
        IViewportModeService viewport,
        IImageLoader loader,
        IThumbnailService thumbnails,
        IDisplayImageFactory<TImage> factory,
        ImageStoreOptions? options = null)
    {
        _catalog = catalog;
        _viewport = viewport;
        _loader = loader;
        _thumbnails = thumbnails;
        _factory = factory;
        _options = options ?? new ImageStoreOptions();

        _session = StartSession();
        _catalog.ImagesChanged += OnCatalogImagesChanged;
        _viewport.ModeChanged += OnViewportModeChanged;
        _viewport.CurrentImageChanged += OnViewportCurrentImageChanged;
        UpdateWindow();
    }

    public IReadOnlyList<ImageReference> Images => _session.Images;

    public event EventHandler? ImagesReset;

    public event EventHandler<int>? ThumbnailChanged;

    public event EventHandler<int>? FullImageChanged;

    public ImageSlot<TImage> GetThumbnail(int index)
        => (uint)index < (uint)_session.Entries.Length ? _session.Entries[index].Thumbnail : ImageSlot<TImage>.NotLoaded;

    public ImageSlot<TImage> GetFullImage(int index)
        => (uint)index < (uint)_session.Entries.Length ? _session.Entries[index].Full : ImageSlot<TImage>.NotLoaded;

    private void OnCatalogImagesChanged(object? sender, EventArgs e)
    {
        var previous = _session;
        previous.Cancellation.Cancel();
        _session = StartSession();

        ImagesReset?.Invoke(this, EventArgs.Empty);

        foreach (var entry in previous.Entries)
        {
            DisposeImage(entry.Thumbnail.Image);
            DisposeImage(entry.Full.Image);
        }

        UpdateWindow();
    }

    private void OnViewportModeChanged(object? sender, ViewportMode e) => UpdateWindow();

    private void OnViewportCurrentImageChanged(object? sender, EventArgs e) => UpdateWindow();

    private Session StartSession()
    {
        var images = _catalog.Images;
        var session = new Session(images, images.Select(static r => new Entry(r)).ToArray());
        if (session.Entries.Length > 0)
        {
            _ = GenerateThumbnailsAsync(session);
        }

        return session;
    }

    // ---- Full-size sliding window -------------------------------------------------------------

    /// <summary>
    /// Reconciles the loaded full-size images with the viewport: current ± 1 in single mode, nothing otherwise.
    /// Safe to call in any event order — when the viewport has already moved to a catalog the store hasn't
    /// reset for yet, it waits for the catalog event.
    /// </summary>
    private void UpdateWindow()
    {
        var session = _session;
        if (_disposed || !ReferenceEquals(session.Images, _catalog.Images))
        {
            return;
        }

        var desired = new List<int>(3);
        var current = _viewport.CurrentIndex;
        if (_viewport.Mode == ViewportMode.Single && (uint)current < (uint)session.Entries.Length)
        {
            desired.Add(current); // loaded first
            if (current > 0)
            {
                desired.Add(current - 1);
            }

            if (current < session.Entries.Length - 1)
            {
                desired.Add(current + 1);
            }
        }

        foreach (var index in session.WindowIndices.Where(i => !desired.Contains(i)).ToList())
        {
            ReleaseFull(session, index);
        }

        foreach (var index in desired)
        {
            if (session.WindowIndices.Add(index))
            {
                _ = LoadFullAsync(session, index);
            }
        }
    }

    private void ReleaseFull(Session session, int index)
    {
        var entry = session.Entries[index];
        session.WindowIndices.Remove(index);
        entry.FullLoad?.Cancel();
        entry.FullLoad = null;

        var previous = entry.Full;
        entry.Full = ImageSlot<TImage>.NotLoaded;
        FullImageChanged?.Invoke(this, index);
        DisposeImage(previous.Image);
    }

    private async Task LoadFullAsync(Session session, int index)
    {
        var entry = session.Entries[index];
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation.Token);
        entry.FullLoad = cts;
        entry.Full = ImageSlot<TImage>.Loading;
        session.BeginFullLoad();
        FullImageChanged?.Invoke(this, index);

        // A full-size decode is also the cheapest way to a missing thumbnail.
        var wantThumbnail = entry.Thumbnail.State != ImageLoadState.Ready;
        var token = cts.Token;

        try
        {
            var (full, thumbnail) = await Task.Run(
                () =>
                {
                    var source = _loader.Load(entry.Reference, token);
                    token.ThrowIfCancellationRequested();
                    var fullImage = _factory.Create(source.Buffer);
                    try
                    {
                        var thumbnailImage = wantThumbnail
                            ? _factory.Create(_thumbnails.CreateThumbnail(source.Buffer, _options.ThumbnailMaxEdge))
                            : null;
                        return (fullImage, thumbnailImage);
                    }
                    catch
                    {
                        DisposeImage(fullImage);
                        throw;
                    }
                },
                token);

            if (!IsLive(session) || entry.FullLoad != cts)
            {
                // Released (or superseded) while decoding.
                DisposeImage(full);
                DisposeImage(thumbnail);
                return;
            }

            entry.Full = ImageSlot<TImage>.Ready(full);
            FullImageChanged?.Invoke(this, index);

            if (thumbnail is not null)
            {
                OfferThumbnail(session, index, thumbnail);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (IsLive(session) && entry.FullLoad == cts)
            {
                entry.Full = ImageSlot<TImage>.Failed(ImageLoadErrors.Describe(entry.Reference, ex));
                FullImageChanged?.Invoke(this, index);
            }
        }
        finally
        {
            if (entry.FullLoad == cts)
            {
                entry.FullLoad = null;
            }

            session.EndFullLoad();
        }
    }

    // ---- Background thumbnails ------------------------------------------------------------------

    private async Task GenerateThumbnailsAsync(Session session)
    {
        var token = session.Cancellation.Token;
        var next = 0;

        async Task WorkerAsync()
        {
            while (true)
            {
                // Full-size window loads (what the user is looking at) go first.
                await session.WaitForFullLoadsIdleAsync(token);

                if (next >= session.Entries.Length)
                {
                    return;
                }

                var index = next++;
                var entry = session.Entries[index];
                if (entry.Thumbnail.State == ImageLoadState.Ready)
                {
                    continue; // produced by a full-size load
                }

                SetThumbnail(session, index, ImageSlot<TImage>.Loading);
                try
                {
                    var thumbnail = await Task.Run(
                        () =>
                        {
                            var source = _loader.Load(entry.Reference, token);
                            token.ThrowIfCancellationRequested();
                            return _factory.Create(_thumbnails.CreateThumbnail(source.Buffer, _options.ThumbnailMaxEdge));
                        },
                        token);

                    if (!IsLive(session))
                    {
                        DisposeImage(thumbnail);
                        return;
                    }

                    OfferThumbnail(session, index, thumbnail);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (IsLive(session) && entry.Thumbnail.State != ImageLoadState.Ready)
                    {
                        SetThumbnail(session, index, ImageSlot<TImage>.Failed(ImageLoadErrors.Describe(entry.Reference, ex)));
                    }
                }
            }
        }

        try
        {
            var workers = Math.Max(1, _options.MaxConcurrentThumbnailLoads);
            await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => WorkerAsync()));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Folder replaced or store disposed.
        }
    }

    /// <summary>Stores <paramref name="thumbnail"/> unless one is already ready (then it is discarded).</summary>
    private void OfferThumbnail(Session session, int index, TImage thumbnail)
    {
        if (session.Entries[index].Thumbnail.State == ImageLoadState.Ready)
        {
            DisposeImage(thumbnail);
            return;
        }

        SetThumbnail(session, index, ImageSlot<TImage>.Ready(thumbnail));
    }

    private void SetThumbnail(Session session, int index, ImageSlot<TImage> slot)
    {
        var entry = session.Entries[index];
        var previous = entry.Thumbnail;
        entry.Thumbnail = slot;
        ThumbnailChanged?.Invoke(this, index);

        if (!ReferenceEquals(previous.Image, slot.Image))
        {
            DisposeImage(previous.Image);
        }
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private bool IsLive(Session session) => !_disposed && ReferenceEquals(session, _session);

    private static void DisposeImage(TImage? image) => (image as IDisposable)?.Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog.ImagesChanged -= OnCatalogImagesChanged;
        _viewport.ModeChanged -= OnViewportModeChanged;
        _viewport.CurrentImageChanged -= OnViewportCurrentImageChanged;
        _session.Cancellation.Cancel();

        foreach (var entry in _session.Entries)
        {
            DisposeImage(entry.Thumbnail.Image);
            DisposeImage(entry.Full.Image);
        }
    }

    private sealed class Entry(ImageReference reference)
    {
        public ImageReference Reference { get; } = reference;

        public ImageSlot<TImage> Thumbnail { get; set; }

        public ImageSlot<TImage> Full { get; set; }

        /// <summary>Cancellation for the in-flight full-size load; identifies which load is current.</summary>
        public CancellationTokenSource? FullLoad { get; set; }
    }

    /// <summary>Everything belonging to one catalog snapshot; replaced wholesale when the folder changes.</summary>
    private sealed class Session(IReadOnlyList<ImageReference> images, Entry[] entries)
    {
        private int _activeFullLoads;
        private TaskCompletionSource? _fullLoadsIdle;

        public IReadOnlyList<ImageReference> Images { get; } = images;

        public Entry[] Entries { get; } = entries;

        // Not disposed: linked load sources may still unregister from it after a folder change.
        public CancellationTokenSource Cancellation { get; } = new();

        /// <summary>Indices in the full-size window (loading, ready, or failed).</summary>
        public HashSet<int> WindowIndices { get; } = [];

        public void BeginFullLoad() => _activeFullLoads++;

        public void EndFullLoad()
        {
            if (--_activeFullLoads == 0 && _fullLoadsIdle is { } idle)
            {
                _fullLoadsIdle = null;
                idle.TrySetResult();
            }
        }

        public Task WaitForFullLoadsIdleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_activeFullLoads == 0)
            {
                return Task.CompletedTask;
            }

            // Asynchronous continuations: waiting workers must not resume inside EndFullLoad's caller.
            _fullLoadsIdle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _fullLoadsIdle.Task.WaitAsync(cancellationToken);
        }
    }
}
