using System.Text;
using Puissance4.Cli;

Console.OutputEncoding = Encoding.UTF8;

GameSetup? setup = GameSetup.FromArguments(args);
if (setup is null)
{
    GameSetup.PrintUsage();
    return 0;
}

new ConsoleGame(setup).Run();
return 0;
