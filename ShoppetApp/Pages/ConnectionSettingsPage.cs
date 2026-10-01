using ShoppetApp.Services;
namespace ShoppetApp.Pages;
public class ConnectionSettingsPage:ContentPage
{
 public ConnectionSettingsPage(ApiService api)
 {
  Title="Server connection";BackgroundColor=Color.FromArgb("#fdfcf9");
  var endpoint=new Entry{Text=api.CurrentBaseUrl,Placeholder="http://10.0.2.2:5020/api",Keyboard=Keyboard.Url};
  var web=new Entry{Text=Preferences.Get("PublicWebBaseUrl","http://localhost:5253"),Placeholder="http://YOUR-PC-IP:5253",Keyboard=Keyboard.Url};
  var btnEmulator = new Button { Text = "🤖 Android Emulator Preset (10.0.2.2)", BackgroundColor = Color.FromArgb("#e8f4f3"), TextColor = Color.FromArgb("#246e6d"), CornerRadius = 14, HeightRequest = 40, FontFamily = "NunitoBold", FontSize = 12 };
  var btnLocalhost = new Button { Text = "💻 Windows / Mac Preset (127.0.0.1)", BackgroundColor = Color.FromArgb("#e8f4f3"), TextColor = Color.FromArgb("#246e6d"), CornerRadius = 14, HeightRequest = 40, FontFamily = "NunitoBold", FontSize = 12 };

  btnEmulator.Clicked += (_, _) => {
      endpoint.Text = "http://10.0.2.2:5020/api";
      web.Text = "http://10.0.2.2:5253";
  };
  btnLocalhost.Clicked += (_, _) => {
      endpoint.Text = "http://localhost:5020/api";
      web.Text = "http://localhost:5253";
  };

  var save=new Button{Text="Save connection",BackgroundColor=Color.FromArgb("#246e6d"),TextColor=Colors.White,CornerRadius=20,HeightRequest=48};
  save.Clicked+=async(_,_)=>{try{api.ConfigureConnection(endpoint.Text??"",web.Text??"");await DisplayAlertAsync("Connection saved","Use the same reachable web address in the API PublicWebBaseUrl setting for QR codes.","OK");}catch(ArgumentException ex){await DisplayAlertAsync("Invalid address",ex.Message,"OK");}};
  Content=new ScrollView{Content=new VerticalStackLayout{Padding=24,Spacing=14,Children={
      new Label{Text="Connect Web + Mobile",FontFamily="NunitoExtraBold",FontSize=26,TextColor=Color.FromArgb("#204d4c")},
      new Label{Text="Quick presets to avoid typing:",FontSize=13,TextColor=Color.FromArgb("#555555")},
      btnEmulator,
      btnLocalhost,
      new Label{Text="API base URL (include /api)",FontFamily="NunitoBold",FontSize=13},
      endpoint,
      new Label{Text="Public web URL for Pet ID links",FontFamily="NunitoBold",FontSize=13},
      web,
      save
  }}};
 }
}
