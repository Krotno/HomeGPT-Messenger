using System.Diagnostics;
using System.Threading.Tasks;

namespace HomeGPT_Messenger.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        MessagingCenter.Subscribe<App, string>(this, "", (app, mode) =>
        {
            ThemeButton.Text = $"Тема: Системная ({mode})";
        });
        UpdateThemeButtonText();
        SetFontSize(Preferences.Get("fontSize", "system")); //для подтягивание текущих значений  
        ShowTime.IsToggled = Preferences.Get("showTime", true);
        var ip = Preferences.Get("llm_ip", "");
        if (!string.IsNullOrWhiteSpace(ip))
        {
            IpEntry.Placeholder = Preferences.Get("llm_ip", "");//Ip
        }
       
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var ip = Preferences.Get("llm_ip", "");
        if (!string.IsNullOrWhiteSpace(ip)) PingLabel.Text = await PingOllamaAsync(ip);
    }
    #region Overlay (для меню)
    private void OnMenuOverlayTapped(object sender, EventArgs e)
    {
        SideMenu.IsVisible = false;
    }
    #endregion

    #region ButtonMenu (Кнопки меню)
    private void OnMenuButtonClicked(object sender, EventArgs e)
    {
        SideMenu.IsVisible = !SideMenu.IsVisible;
    }

    private async void OnChatsClicked(object sender, EventArgs e)
    {
        SideMenu.IsVisible = false;
        await Navigation.PushAsync(new ChatsPage());

    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        SideMenu.IsVisible = false;
        await Navigation.PushAsync(new SettingsPage());
    }
    #endregion

    #region Theme(Системная/Светлая/Тёмная)
    private async void OnThemeButtonClicked(object sender, EventArgs e)
    {
        string result = await DisplayActionSheet("Выберите тему", "Отмена", null,"Системная","Светлая","Тёмная");
        if (result == null || result == "Отмена") return;
        if (Application.Current is App app)
        {
            if (result == "Системная")
            {
                Preferences.Set("theme", "system");
                var isDark = Application.Current.RequestedTheme == AppTheme.Dark;
                app.SetThem(isDark);
                ThemeButton.Text = "Тема:Системная";

            }
            else if (result == "Светлая")
            {
                Preferences.Set("theme", "light");
                app.SetThem(false);
                ThemeButton.Text = "Тема:Светлая";
            }
            else if (result == "Тёмная")
            {
                Preferences.Set("theme", "dark");
                app.SetThem(true);
                ThemeButton.Text = "Тема:Тёмная";
            }
            UpdateThemeButtonText();
        } 
    }

    private void UpdateThemeButtonText()
    {
        string pref = Preferences.Get("theme", "system");
        if (pref == "system")
        {
            var mode = Application.Current.RequestedTheme == AppTheme.Dark ? "Тёмная" : "Светлая";
            ThemeButton.Text = $"Тема: Системная({mode})";
        }else if (pref == "light")
        {
            ThemeButton.Text = "Тема: Светлая";
        }else if (pref == "dark")
        {
            ThemeButton.Text = "Тема: Тёмная";
        }
    }
    #endregion

    #region FontSize (Размер текста)
    private void OnFontSizeChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return;
        if (sender == SmallFontRB)
        {
            SetFontSize("small");
        }else if (sender == MediumFontRB)
        {
            SetFontSize("medium");
        }else if (sender == LargeFontRB)
        {
            SetFontSize("large");
        }
    }

    private void OnResetFontSizeClicked(object sender, EventArgs e)
    {
        SetFontSize("system");
        SmallFontRB.IsChecked=false;
        MediumFontRB.IsChecked=false;
        LargeFontRB.IsChecked=false;
    }

    private void SetFontSize(string mode)
    {
        Preferences.Set("fontSize", mode);
        (Application.Current as App)?.ApplyFontSizeSetting();
        CurrentFontSizeLabel.Text = "Текущий размер: " + (mode switch
        {
            "small" => "Маленький",
            "medium" => "Средний",
            "large" => "Большой",
            _ => "Системный"
        });
    }
    #endregion

    private void OnShowTimeToggle(object sender, ToggledEventArgs e)
    {
        Preferences.Set("showTime", e.Value);
    }

    #region IP (Сохранения/сброс/пинг)
    private async void OnSaveIpClicked(object sender, EventArgs e)
    {
        var ip = IpEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(ip))
        {
            DisplayAlert("Ошибка", "Введите корректный IP и порт, например 192.168.1.100:11434", "Ок");
            return;
        }

        Preferences.Set("llm_ip", ip);
        DisplayAlert("Успешно","Адрес сохранен","Ок");
        IpEntry.Placeholder= Preferences.Get("llm_ip", "");
        PingLabel.Text= await PingOllamaAsync(ip);
    }

    private void OnResetIpClicked(object sender, EventArgs e)
    {
        IpEntry.Text = Preferences.Get("llm_ip","");
        DisplayAlert("Сброс", "Адрес сброшен", "Ок");
    }


    private async Task<string> PingOllamaAsync(string ip)
    {
        try
        {
            var baseUrl = $"http://{ip}";

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(3) };

            var sw = Stopwatch.StartNew();

            var resp = await http.GetAsync("/api/tags");

            sw.Stop();

            if (!resp.IsSuccessStatusCode) return $"Ошибка ping ({(int)resp.StatusCode})";

            return $"Ок ({sw.ElapsedMilliseconds} ms)";
        }
        catch (Exception ex)
        {
            Device.BeginInvokeOnMainThread(() =>
            {

                DisplayAlert("Ошибка ping", ex.Message, "Ок");

            });
            return "Ошибка";
        }
    }

    #endregion


}