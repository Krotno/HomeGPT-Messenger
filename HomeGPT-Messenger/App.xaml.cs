using HomeGPT_Messenger.Pages;

namespace HomeGPT_Messenger
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            string theme = Preferences.Get("theme", "system");
            if (theme == "dark")
            {
                SetThem(true);
            }else if (theme == "light")
            {
                SetThem(false);
            }else //System
            {
                var isDark=Application.Current.RequestedTheme==AppTheme.Dark;
                SetThem(isDark);  
            }
            Application.Current.RequestedThemeChanged += OnSystemThemeChange;
            MainPage = new NavigationPage(new ChatsPage());
        }
        #region Theme(Системная/Тёмная/Светлая)
        public void SetThem(bool dark)
        {

            Resources.MergedDictionaries.Clear();
            if (dark)
            {
                Resources.MergedDictionaries.Add(new Themes.Dark());
            }else
            {
                Resources.MergedDictionaries.Add(new Themes.Light());
            }

        }

        private void OnSystemThemeChange(object sender, AppThemeChangedEventArgs e)
        {
            if (Preferences.Get("theme", "system") == "system")
            {
                bool isDark=e.RequestedTheme==AppTheme.Dark;
                SetThem(isDark);

                MessagingCenter.Send(this, "ThemeChanged", isDark ? "Тёмная" : "Светлая");
            }
        }
        #endregion

        public void ApplyFontSizeSetting()
        {
            var fontPref = Preferences.Get("fontSize", "system");
            double size = fontPref switch
            {
                "small" => 12,
                "medium" => 16,
                "large" => 20,
                _ => Device.GetNamedSize(NamedSize.Default, typeof(Label))
            };
            Application.Current.Resources["AppFontSize"] = size;
        }
    }
}