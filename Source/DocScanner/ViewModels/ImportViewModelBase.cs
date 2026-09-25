using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;
using DocScanner.Services;

namespace DocScanner.ViewModels;

/// <summary>Busy overlay + cancel + result messages shared by every screen that imports photos.</summary>
public abstract partial class ImportViewModelBase : ObservableObject
{
	private CancellationTokenSource? _cts;

	/// <summary>True only while an import runs. <see cref="Progress{T}"/> posts its callbacks to the UI
	/// thread, so the last few can arrive after the import already returned and turned the overlay off;
	/// without this guard such a late callback would switch it back on and it would never go away.</summary>
	private bool _importing;

	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private string busyText = "";

	[RelayCommand]
	private void CancelImport() => _cts?.Cancel();

	/// <summary>Runs a pick + import. The overlay only appears once photos were actually
	/// picked (first progress report), not while the system picker is open.</summary>
	protected async Task<ImportOutcome?> RunImportAsync(
		Func<IProgress<ImportProgress>, CancellationToken, Task<ImportOutcome?>> import)
	{
		_cts = new CancellationTokenSource();
		_importing = true;
		var progress = new Progress<ImportProgress>(p =>
		{
			if (!_importing) return;
			IsBusy = true;
			BusyText = p.Total <= 1
				? "Đang nhập ảnh..."
				: $"Đang nhập ảnh {Math.Min(p.Done + 1, p.Total)}/{p.Total}";
		});
		try
		{
			ImportOutcome? outcome = await import(progress, _cts.Token);
			_importing = false;
			IsBusy = false;
			if (outcome != null) await ReportAsync(outcome.Result);
			return outcome;
		}
		finally
		{
			_importing = false;
			IsBusy = false;
			_cts.Dispose();
			_cts = null;
		}
	}

	private static async Task ReportAsync(ImportResult r)
	{
		if (r.Failures.Count > 0)
		{
			string names = string.Join("\n", r.Failures.Take(5).Select(f => "• " + f.Name));
			string more = r.Failures.Count > 5 ? $"\n... và {r.Failures.Count - 5} ảnh khác" : "";
			await Shell.Current.DisplayAlertAsync("Có ảnh không đọc được",
				$"Không nhập được {r.Failures.Count} ảnh:\n{names}{more}", "OK");
		}
		if (r.Cancelled)
			await Shell.Current.DisplayAlertAsync("Đã huỷ", $"Đã nhập {r.Added} ảnh trước khi huỷ.", "OK");
	}
}
