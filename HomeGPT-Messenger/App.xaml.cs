using HomeGPT_Messenger.Pages;

namespace HomeGPT_Messenger
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            //MainPage = new MainPage();
            MainPage = new NavigationPage(new ChatsPage());
        }

        public void SetThem(String theme)
        {
            Resources.MergedDictionaries.Clear();
            if (theme != null)
                Resources.MergedDictionaries.Add(new Themes.Dark());
            else
                Resources.MergedDictionaries.Add(new Themes.Light());
        }

        //protected override Window CreateWindow(IActivationState? activationState)
        //{
        //    return new Window(new AppShell());
        //}
    }
}