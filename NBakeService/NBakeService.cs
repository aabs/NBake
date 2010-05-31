using System.ServiceProcess;

namespace NBakeService
{
    public partial class NBakeService : ServiceBase
    {

        public NBakeService()
        {
            InitializeComponent();
            Baker = new Baker();
        }

        protected Baker Baker { get; set; }

        public void RunInConsoleMode(string[] args)
        {
            OnStart(args);
        }

        protected override void OnStart(string[] args)
        {
            Baker.OnStart(args);
        }

        protected override void OnStop()
        {
            Baker.OnStop();
        }
    }
}