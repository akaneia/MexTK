namespace MexTK.Commands
{
    public interface ICommand
    {
        string Name();

        string ID();

        string Help();

        bool DoIt(string[] args);
    }
}
