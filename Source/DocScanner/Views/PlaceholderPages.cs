namespace DocScanner.Views;

/// <summary>Empty screens that reserve the navigation routes; each is replaced by the real
/// page in its own step.</summary>
public abstract class PlaceholderPage : ContentPage
{
	protected PlaceholderPage(string title, string text)
	{
		Title = title;
		Content = new Label
		{
			Text = text,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.Center,
			Margin = new Thickness(24),
		};
	}
}

public sealed class PageViewerPage() : PlaceholderPage("Xem trang", "Xem và quản lý các trang (Bước 8).");

public sealed class CropPage() : PlaceholderPage("Cắt tài liệu", "Dò mép giấy và kéo 4 điểm (Bước 4-6).");

public sealed class ExportPage() : PlaceholderPage("Xuất PDF", "Xuất PDF nhiều trang và chia sẻ (Bước 8).");
