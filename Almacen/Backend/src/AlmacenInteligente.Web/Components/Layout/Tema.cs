using MudBlazor;

namespace AlmacenInteligente.Web.Components.Layout;

internal static class Tema
{
    private static readonly string[] Fuente = ["system-ui", "-apple-system", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    public static readonly MudTheme Almacen = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1F5F8B",
            Secondary = "#5B6B7A",
            Tertiary = "#2E8B57",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1B2733",
            DrawerBackground = "#14324A",
            DrawerText = "#DCE6EF",
            DrawerIcon = "#9FB6C9",
            Background = "#F4F6F8",
            Surface = "#FFFFFF",
            TextPrimary = "#1B2733",
            TextSecondary = "#5B6B7A",
            LinesDefault = "#E1E6EB",
            TableLines = "#E9EDF1",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#6BA8D6",
            Secondary = "#9AA9B7",
            Tertiary = "#5CC08A",
            AppbarBackground = "#18222C",
            DrawerBackground = "#111A22",
            Background = "#0E151B",
            Surface = "#18222C",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Fuente },
            H4 = new H4Typography { FontFamily = Fuente, FontWeight = "600", FontSize = "1.6rem" },
            H5 = new H5Typography { FontFamily = Fuente, FontWeight = "600" },
            H6 = new H6Typography { FontFamily = Fuente, FontWeight = "600" },
            Button = new ButtonTypography { FontFamily = Fuente, FontWeight = "600", TextTransform = "none" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px", DrawerWidthLeft = "248px" },
    };
}
