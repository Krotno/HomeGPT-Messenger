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
            MainPage = new NavigationPage(new ChatsPage());
        }

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
            //Resources.MergedDictionaries.Clear();
            //if (dark)
            //{
            //    Resources.MergedDictionaries.Add(new Themes.Dark());
            //}
            //else
            //{
            //    Resources.MergedDictionaries.Add(new Themes.Light());
            //}

        }
    }
}