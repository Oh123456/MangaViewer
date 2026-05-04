namespace Viewer;

public static class AppIcons
{
    private static Icon? currentIcon;

    public static Icon? Current
    {
        get
        {
            if (currentIcon is not null)
            {
                return currentIcon;
            }

            try
            {
                currentIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                currentIcon = null;
            }

            return currentIcon;
        }
    }

    public static void ApplyTo(Form form)
    {
        var appIcon = Current;
        if (appIcon is not null)
        {
            form.Icon = appIcon;
        }
    }
}
