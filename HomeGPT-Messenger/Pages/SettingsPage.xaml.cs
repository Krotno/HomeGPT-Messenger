using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.NetworkInformation;

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
        var ipSaved = Preferences.Get("llm_ip", "");

        IpEntry.Placeholder = string.IsNullOrWhiteSpace(ipSaved) ? "192.168.3.77" : ipSaved;

        var llmPortSaved = Preferences.Get("llm_port", "");

        LlmPortEntry.Placeholder = llmPortSaved.ToString();

        var sdPortSaved = Preferences.Get("sd_port", "");

        SdPortEntry.Placeholder = sdPortSaved.ToString();

    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var ip = string.IsNullOrWhiteSpace(IpEntry.Text)? IpEntry.Placeholder:IpEntry.Text;

        if (!string.IsNullOrWhiteSpace(ip)) PingLabel.Text = await PingIpAsync(ip);
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
        var ipSaved = Preferences.Get("llm_ip", "");
        var llmPortSaved = Preferences.Get("llm_port", 11434);
        var sdPortSaved = Preferences.Get("sd_port", 25566);

        var ip = string.IsNullOrWhiteSpace(IpEntry.Text) ? ipSaved : IpEntry.Text.Trim();
        var llmPort = int.TryParse(LlmPortEntry.Text, out var lp) ? lp : llmPortSaved;
        var sdPort = int.TryParse(SdPortEntry.Text, out var sp) ? sp : sdPortSaved;

        if (string.IsNullOrWhiteSpace(ip))
        {
            await DisplayAlert("Ошибка", "Введите IP (Например 192.168.3.77)", "Ок");

            return;
        }
        if (llmPort is < 1 or > 65535)
        {
            await DisplayAlert("Ошибка", "Некорректный порт LLM", "Ок");

            return;
        }
        if (sdPort is < 1 or > 65535)
        {
            await DisplayAlert("Ошибка", "Некорректный порт SD", "Ок");

            return;
        }

        if (ip != ipSaved) Preferences.Set("llm_ip", ip);
        if (sdPort != sdPortSaved) Preferences.Set("sd_port", sdPort);
        if (llmPort != llmPortSaved) Preferences.Set("llm_port", llmPort);

        PingLabel.Text= await PingIpAsync(ip);

        await DisplayAlert("Ок", $"LLM: {ip}:{llmPort}\n SD: {ip}:{sdPort}", "Ок");
    }

    private async void OnResetIpClicked(object sender, EventArgs e)
    {
        IpEntry.Text = LlmPortEntry.Text = SdPortEntry.Text = string.Empty;

        IpEntry.Placeholder = Preferences.Get("llm_ip", "");

        LlmPortEntry.Placeholder = Preferences.Get("llm_port", 11434).ToString();

        SdPortEntry.Placeholder = Preferences.Get("sd_port", 25566).ToString();

        var ip = string.IsNullOrWhiteSpace(IpEntry.Text) ? IpEntry.Placeholder : IpEntry.Text;

        if (!string.IsNullOrWhiteSpace(ip)) PingLabel.Text = await PingIpAsync(ip);
    }


    private async Task<string> PingIpAsync(string ip)
    {
        try
        {
            using var p = new Ping(); var r = await p.SendPingAsync(ip, 2000);

            return r.Status == IPStatus.Success ? $"Ок({r.RoundtripTime} ms)" : "Ошибка";
        }
        catch
        {
            return "Ошибка";
        }
    }

    private string EffectiveIp()=> string.IsNullOrWhiteSpace(IpEntry.Text)? IpEntry.Placeholder: IpEntry.Text;

    private int EffectivePort(Entry e, string prefKey, int defVal)
    {
        if (int.TryParse(e.Text, out var p)) return p;

        if(int.TryParse(e.Placeholder, out var ph)) return ph;

        return Preferences.Get(prefKey, defVal);
    }
    #endregion

}