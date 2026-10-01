using ShoppetApp.Services;
namespace ShoppetApp.Pages;
public class ConnectionSettingsPage:ContentPage
{
 public ConnectionSettingsPage(ApiService api)
 {
  Title="Server connection";BackgroundColor=Color.FromArgb("#fdfcf9");
  var endpoint=new Entry{Text=api.CurrentBaseUrl,Placeholder="http://10.0.2.2:5020/api",Keyboard=Keyboard.Url};
  var web=new Entry{Text=Preferences.Get("PublicWebBaseUrl","http://localhost:5253"),Placeholder="http://YOUR-PC-IP:5253",Keyboard=Keyboard.Url};
  var save=new Button{Text="Save connection",BackgroundColor=Color.FromArgb("#246e6d"),TextColor=Colors.White,CornerRadius=20};
  save.Clicked+=async(_,_)=>{try{api.ConfigureConnection(endpoint.Text??"",web.Text??"");await DisplayAlertAsync("Connection saved","Use the same reachable web address in the API PublicWebBaseUrl setting for QR codes.","OK");}catch(ArgumentException ex){await DisplayAlertAsync("Invalid address",ex.Message,"OK");}};
  Content=new ScrollView{Content=new VerticalStackLayout{Padding=24,Spacing=16,Children={new Label{Text="Connect Web + Mobile",FontFamily="NunitoExtraBold",FontSize=28,TextColor=Color.FromArgb("#204d4c")},new Label{Text="Android emulator: 10.0.2.2 connects to your PC. Physical phone: use your PC's LAN IP and keep both devices on the same network."},new Label{Text="API base URL (include /api)"},endpoint,new Label{Text="Public web URL for Pet ID links"},web,save}}};
 }
}
