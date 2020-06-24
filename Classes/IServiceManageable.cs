
namespace ASTAWebServer
{
    public interface IServiceManageable
    {
        void OnStart();
        void OnStop();
        void OnPause();
        void AddInfo(string text);
    }
}
