using System.Net;
using System.Windows;

namespace SMTAlert
{
    /// <summary>
    /// ESI SSO login window using PKCE authorization flow.
    /// </summary>
    public partial class LogonWindow : Window
    {
        private HttpListener _listener;
        private bool _serverDone;

        public AlertCharacter AddedCharacter { get; private set; }

        public LogonWindow()
        {
            InitializeComponent();
            new Task(StartServer).Start();
        }

        private void StartServer()
        {
            _listener = new HttpListener();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            string challengeCode = Guid.NewGuid().ToString("N")[..32];
            string esiLogonURL = App.CharacterMgr.GetESILogonURL(challengeCode);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(esiLogonURL)
            { UseShellExecute = true });

            try
            {
                _listener.Prefixes.Add(SMT.EVEData.EveAppConfig.CallbackURL);
                _listener.Start();

                while (!_serverDone)
                {
                    var context = _listener.GetContext();
                    var request = context.Request;

                    Dispatcher.Invoke(async () =>
                    {
                        AddedCharacter = await App.CharacterMgr.HandleEveAuthCallback(
                            request.Url, challengeCode);
                        if (AddedCharacter != null)
                        {
                            App.ActiveCharacter = AddedCharacter;
                        }
                    });

                    string responseString = "<HTML><HEAD title=\"SMTAlert Auth\"><meta http-equiv=\"refresh\" " +
                        $"content=\"1;url={esiLogonURL}\"></HEAD><BODY>SMTAlert Character Added.</HTML>";

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    var response = context.Response;
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                }
            }
            catch { }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                _serverDone = true;
                if (_listener != null && _listener.IsListening)
                    _listener.Stop();
            }
            catch { }
        }
    }
}
