using System.ServiceProcess;

namespace ASTAWebServer
{
    public partial class ASTAWebServer : ServiceBase
    {
        IServiceManageable _serviceManagable;

        public ASTAWebServer(IServiceManageable serviceManagable)
        {
            InitializeComponent();
            _serviceManagable = serviceManagable;
        }

        protected override void OnStart(string[] args)
        {
            _serviceManagable.OnStart();
        }

        protected override void OnStop()
        {
            _serviceManagable.OnStop();
        }
    }
}