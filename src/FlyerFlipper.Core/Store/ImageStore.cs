using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
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
    private readonly IImageProcessingPipeline _pipeline;
    private readonly IThumbnailService _thumbnails;
    private readonly IDisplayImageFactory<TImage> _factory;
    private readonly ImageStoreOptions _options;

    private Session _session;

    /// <summary>
    /// Processing generation: bumped whenever processor settings change. A slot produced under an older
    /// generation is stale and gets regenerated.
    /// </summary>
    private int _generation;

    private bool _disposed;

    public ImageStore(
        IImageCatalog catalog,
        IViewportModeService viewport,
        IImageLoader loader,
        IImageProcessingPipeline pipeline,
        IThumbnailService thumbnails,
        IDisplayImageFactory<TImage> factory,
        ImageStoreOptions? options = null)
    {
        _catalog = catalog;
        _viewport = viewport;
        _loader = loader;
        _pipeline = pipeline;
        _thumbnails = thumbnails;
        _factory = factory;
        _options = options ?? new ImageStoreOptions();

        _session = new Session(_catalog.Images);
        StartThumbnailPass(_session);
        _catalog.ImagesChanged += OnCatalogImagesChanged;
        _viewport.ModeChanged += OnViewportModeChanged;
        _viewport.CurrentImageChanged += OnViewportCurrentImageChanged;
        _pipeline.SettingsChanged += OnPipelineSettingsChanged;
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
        _session = new Session(_catalog.Images);

        ImagesReset?.Invoke(this, EventArgs.Empty);

        foreach (var entry in previous.Entries)
        {
            DisposeImage(entry.Thumbnail.Image);
            DisposeImage(entry.Full.Image);
        }

        StartThumbnailPass(_session);
        UpdateWindow();
    }

    private void OnViewportModeChanged(object? sender, ViewportMode e) => UpdateWindow();

    private void OnViewportCurrentImageChanged(object? sender, EventArgs e) => UpdateWindow();

    /// <summary>
    /// Processor settings changed: every processed image is stale. Reload the full-size window first
    /// (it has priority), then regenerate all thumbnails. Current images stay visible until replaced.
    /// </summary>
    private void OnPipelineSettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _generation++;
        var session = _session;
        foreach (var index in session.WindowIndices.ToList())
        {
            _ = LoadFullAsync(session, index);
        }

        StartThumbnailPass(session);
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

        SetFull(index, entry, ImageSlot<TImage>.NotLoaded);
    }

    /// <summary>Loads (or, if already loading/loaded, re-loads) the full-size image, superseding any earlier load.</summary>
    private async Task LoadFullAsync(Session session, int index)
    {
        var entry = session.Entries[index];
        entry.FullLoad?.Cancel();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation.Token);
        var generation = _generation;
        entry.FullLoad = cts;
        session.BeginFullLoad();
        SetFull(index, entry, ImageSlot<TImage>.Refreshing(entry.Full.Image));

        // A full-size decode is also the cheapest way to a missing or stale thumbnail.
        var wantThumbnail = entry.ThumbnailGeneration != generation;
        var token = cts.Token;

        try
        {
            var (full, thumbnail) = await Task.Run(
                () =>
                {
                    var processed = LoadAndProcess(entry.Reference, token);
                    var fullImage = _factory.Create(processed);
                    try
                    {
                        var thumbnailImage = wantThumbnail
                            ? _factory.Create(_thumbnails.CreateThumbnail(processed, _options.ThumbnailMaxEdge))
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
                // Released, superseded by a newer load, or the folder changed while decoding.
                DisposeImage(full);
                DisposeImage(thumbnail);
                return;
            }

            SetFull(index, entry, ImageSlot<TImage>.Ready(full));

            if (thumbnail is not null)
            {
                OfferThumbnail(session, index, thumbnail, generation);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (IsLive(session) && entry.FullLoad == cts)
            {
                SetFull(index, entry, ImageSlot<TImage>.Failed(ImageLoadErrors.Describe(entry.Reference, ex)));
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

    private void SetFull(int index, Entry entry, ImageSlot<TImage> slot)
    {
        var previous = entry.Full;
        entry.Full = slot;
        FullImageChanged?.Invoke(this, index);

        if (!ReferenceEquals(previous.Image, slot.Image))
        {
            DisposeImage(previous.Image);
        }
    }

    // ---- Background thumbnails ------------------------------------------------------------------

    /// <summary>Cancels any running thumbnail pass and starts one for the current generation.</summary>
    private void StartThumbnailPass(Session session)
    {
        session.ThumbnailPass?.Cancel();
        if (session.Entries.Length == 0)
        {
            return;
        }

        var pass = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation.Token);
        session.ThumbnailPass = pass;
        _ = GenerateThumbnailsAsync(session, pass.Token, _generation);
    }

    private async Task GenerateThumbnailsAsync(Session session, CancellationToken token, int generation)
    {
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
                if (entry.ThumbnailGeneration == generation)
                {
                    continue; // already produced for this generation (e.g. by a full-size load)
                }

                SetThumbnail(session, index, ImageSlot<TImage>.Refreshing(entry.Thumbnail.Image));
                try
                {
                    var thumbnail = await Task.Run(
                        () =>
                        {
                            var processed = LoadAndProcess(entry.Reference, token);
                            return _factory.Create(_thumbnails.CreateThumbnail(processed, _options.ThumbnailMaxEdge));
                        },
                        token);

                    if (!IsLive(session) || token.IsCancellationRequested)
                    {
                        DisposeImage(thumbnail);
                        return;
                    }

                    OfferThumbnail(session, index, thumbnail, generation);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (IsLive(session) && generation == _generation && entry.ThumbnailGeneration != generation)
                    {
                        entry.ThumbnailGeneration = generation;
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
            // Folder replaced, settings changed (a newer pass took over), or store disposed.
        }
    }

    /// <summary>
    /// Stores a thumbnail produced under <paramref name="generation"/>, unless that generation is outdated
    /// or this entry already has one for it (then it is discarded).
    /// </summary>
    private void OfferThumbnail(Session session, int index, TImage thumbnail, int generation)
    {
        var entry = session.Entries[index];
        if (generation != _generation || entry.ThumbnailGeneration == generation)
        {
            DisposeImage(thumbnail);
            return;
        }

        entry.ThumbnailGeneration = generation;
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

    /// <summary>
    /// Decode, then run the pipeline — the only way pixels reach either view, so thumbnails and full-size
    /// images always reflect the same processing. Runs on a background thread.
    /// </summary>
    private ImageBuffer LoadAndProcess(ImageReference reference, CancellationToken cancellationToken)
    {
        var source = _loader.Load(reference, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return _pipeline.Process(ProcessedImage.FromSource(source), cancellationToken).Buffer;
    }

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
        _pipeline.SettingsChanged -= OnPipelineSettingsChanged;
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

        /// <summary>Generation the thumbnail slot's Ready/Failed result belongs to; -1 before the first one.</summary>
        public int ThumbnailGeneration { get; set; } = -1;

        public ImageSlot<TImage> Full { get; set; }

        /// <summary>Cancellation for the in-flight full-size load; identifies which load is current.</summary>
        public CancellationTokenSource? FullLoad { get; set; }
    }

    /// <summary>Everything belonging to one catalog snapshot; replaced wholesale when the folder changes.</summary>
    private sealed class Session(IReadOnlyList<ImageReference> images)
    {
        private int _activeFullLoads;
        private TaskCompletionSource? _fullLoadsIdle;

        public IReadOnlyList<ImageReference> Images { get; } = images;

        public Entry[] Entries { get; } = images.Select(static r => new Entry(r)).ToArray();

        // Not disposed: linked load sources may still unregister from it after a folder change.
        public CancellationTokenSource Cancellation { get; } = new();

        /// <summary>The running thumbnail pass (linked to <see cref="Cancellation"/>); replaced on settings changes.</summary>
        public CancellationTokenSource? ThumbnailPass { get; set; }

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
