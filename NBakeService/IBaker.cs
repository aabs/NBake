namespace NBakeService
{
    public interface IBaker
    {
        void OnStart(string[] args);
        void OnStop();
    }
}