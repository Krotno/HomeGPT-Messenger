using HomeGPT_Messenger.Pages;

namespace HomeGPT_Messenger
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new NavigationPage(new ChatsPage());
        }

        public void SetThem(bool dark)
        {
            Resources.MergedDictionaries.Clear();
            if (dark)
            {
                Resources.MergedDictionaries.Add(new Themes.Dark());
            }
            else
            {
                Resources.MergedDictionaries.Add(new Themes.Light());
            }
 
        }
    }
}