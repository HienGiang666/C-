using Microsoft.Maui.Controls;

namespace TourApp.Mobile;

/// <summary>Thay thế gán Application.MainPage (deprecated .NET 9) bằng Window.Page.</summary>
internal static class AppNavigation
{
    public static void SetRootPage(Page page)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0)
            Microsoft.Maui.Controls.Application.Current.Windows[0].Page = page;
    }
}
