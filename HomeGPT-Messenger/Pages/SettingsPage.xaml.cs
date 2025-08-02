//using static ObjCRuntime.Dlfcn;

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

    //private void UpdateFontSizeLabel()
    //{
    //    string current = Preferences.Get("fontSize", "system");
    //    CurrentFontSizeLabel.Text= "������� ������: " + (mode switch
    //    {
    //        "small" => "���������",
    //        "medium" => "�������",
    //        "large" => "�������",
    //        _ => "���������"
    //    });
    //    string text = current switch
    //    {
    //        ""=>"",
    //    }
    //}

}