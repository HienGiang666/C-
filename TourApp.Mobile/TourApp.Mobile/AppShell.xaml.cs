using TourApp.Mobile.Views;
using TourApp.Mobile.Views.Auth;

namespace TourApp.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for pages that aren't in the Shell visual hierarchy (Auth)
            Routing.RegisterRoute("LoginPage", typeof(Views.Auth.LoginPage));
            Routing.RegisterRoute("SignUpPage", typeof(Views.Auth.SignUpPage));
            Routing.RegisterRoute("ForgotPasswordPage", typeof(Views.Auth.ForgotPasswordPage));
            Routing.RegisterRoute("VerificationPage", typeof(Views.Auth.VerificationPage));
        }
    }
}
