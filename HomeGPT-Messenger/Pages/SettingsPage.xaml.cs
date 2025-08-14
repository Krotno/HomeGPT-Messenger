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
            ThemeButton.Text = $"����: ��������� ({mode})";
        });
        UpdateThemeButtonText();
        SetFontSize(Preferences.Get("fontSize", "system")); //��� ������������ ������� ��������  
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
    #region Overlay (��� ����)
    private void OnMenuOverlayTapped(object sender, EventArgs e)
    {
        SideMenu.IsVisible = false;
    }
    #endregion

    #region ButtonMenu (������ ����)
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

    #region Theme(���������/�������/Ҹ����)
    private async void OnThemeButtonClicked(object sender, EventArgs e)
    {
        string result = await DisplayActionSheet("�������� ����", "������", null,"���������","�������","Ҹ����");
        if (result == null || result == "������") return;
        if (Application.Current is App app)
        {
            if (result == "���������")
            {
                Preferences.Set("theme", "system");
                var isDark = Application.Current.RequestedTheme == AppTheme.Dark;
                app.SetThem(isDark);
                ThemeButton.Text = "����:���������";

            }
            else if (result == "�������")
            {
                Preferences.Set("theme", "light");
                app.SetThem(false);
                ThemeButton.Text = "����:�������";
            }
            else if (result == "Ҹ����")
            {
                Preferences.Set("theme", "dark");
                app.SetThem(true);
                ThemeButton.Text = "����:Ҹ����";
            }
            UpdateThemeButtonText();
        } 
    }

    private void UpdateThemeButtonText()
    {
        string pref = Preferences.Get("theme", "system");
        if (pref == "system")
        {
            var mode = Application.Current.RequestedTheme == AppTheme.Dark ? "Ҹ����" : "�������";
            ThemeButton.Text = $"����: ���������({mode})";
        }else if (pref == "light")
        {
            ThemeButton.Text = "����: �������";
        }else if (pref == "dark")
        {
            ThemeButton.Text = "����: Ҹ����";
        }
    }
    #endregion

    #region FontSize (������ ������)
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
        CurrentFontSizeLabel.Text = "������� ������: " + (mode switch
        {
            "small" => "���������",
            "medium" => "�������",
            "large" => "�������",
            _ => "���������"
        });
    }
    #endregion

    private void OnShowTimeToggle(object sender, ToggledEventArgs e)
    {
        Preferences.Set("showTime", e.Value);
    }

    #region IP (����������/�����/����)
    private async void OnSaveIpClicked(object sender, EventArgs e)
    {
        var ip = IpEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(ip))
        {
            DisplayAlert("������", "������� ���������� IP � ����, �������� 192.168.1.100:11434", "��");
            return;
        }

        Preferences.Set("llm_ip", ip);
        DisplayAlert("�������","����� ��������","��");
        IpEntry.Placeholder= Preferences.Get("llm_ip", "");
        PingLabel.Text= await PingOllamaAsync(ip);
    }

    private void OnResetIpClicked(object sender, EventArgs e)
    {
        IpEntry.Text = Preferences.Get("llm_ip","");
        DisplayAlert("�����", "����� �������", "��");
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

            if (!resp.IsSuccessStatusCode) return $"������ ping ({(int)resp.StatusCode})";

            return $"�� ({sw.ElapsedMilliseconds} ms)";
        }
        catch (Exception ex)
        {
            Device.BeginInvokeOnMainThread(() =>
            {

                DisplayAlert("������ ping", ex.Message, "��");

            });
            return "������";
        }
    }

    #endregion


}