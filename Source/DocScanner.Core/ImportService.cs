namespace DocScanner.Core;

/// <summary>One photo to import; the stream is opened lazily, one photo at a time.</summary>
public sealed record ImportSource(string Name, Func<CancellationToken, Task<Stream>> OpenAsync);

public sealed record ImportFailure(string Name, string Message);

public sealed record ImportProgress(int Done, int Total);

/// <param name="Added">Pages that were added to the document (kept even when cancelled).</param>
public sealed record ImportResult(int Added, IReadOnlyList<ImportFailure> Failures, bool Cancelled);

/// <summary>
/// Adds picked / captured photos to a document <b>fast</b>: each original is copied into its page
/// folder and the page is recorded as <see cref="PageState.Pending"/> (and saved), then the heavy
/// work (thumbnail, proxy, outline) is left to <see cref="PageIngestQueue"/>. So the pages appear
/// in the UI right away, one per copied file, and a crash or a cancel keeps whatever was added.
/// </summary>
public sealed class ImportService(DocumentStore store, PageIngestQueue queue)
{
    private static readonly HashSet<string> KnownExtensions = [".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".bmp", ".gif"];

    public async Task<ImportResult> ImportAsync(DocumentRecord doc, IReadOnlyList<ImportSource> sources,
        IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
    {
        int added = 0;
        var failures = new List<ImportFailure>();
        progress?.Report(new ImportProgress(0, sources.Count));

        for (int i = 0; i < sources.Count; i++)
        {
            ImportSource source = sources[i];
            var page = new PageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                OriginalExtension = ExtensionOf(source.Name),
                State = PageState.Pending,
            };
            string folder = store.PageFolder(doc.Id, page.Id);
            try
            {
                ct.ThrowIfCancellationRequested();
                Directory.CreateDirectory(folder);

                await using (Stream input = await source.OpenAsync(ct))
                await using (FileStream output = File.Create(store.OriginalPath(doc.Id, page)))
                    await input.CopyToAsync(output, ct);

                if (!store.Update(doc.Id, d => d.Pages.Add(page)))
                {
                    DeleteQuietly(folder); // the document was deleted meanwhile
                    break;
                }
                queue.Enqueue(doc.Id, page.Id);
                added++;
            }
            catch (OperationCanceledException)
            {
                DeleteQuietly(folder);
                return new ImportResult(added, failures, Cancelled: true);
            }
            catch (Exception ex)
            {
                // One unreadable photo must not sink the rest of the batch.
                DeleteQuietly(folder);
                failures.Add(new ImportFailure(source.Name, ex.Message));
            }
            progress?.Report(new ImportProgress(i + 1, sources.Count));
        }
        return new ImportResult(added, failures, Cancelled: false);
    }

    private static string ExtensionOf(string name)
    {
        string ext = Path.GetExtension(name).ToLowerInvariant();
        return KnownExtensions.Contains(ext) ? ext : ".jpg";
    }

    private static void DeleteQuietly(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
    }
}
