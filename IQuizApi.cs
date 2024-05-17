using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;

namespace QuizApi
{
    public interface IQuizApi
    {
        static PluginCapability<IQuizApi> Capability { get; } = new("quiz:core");
        void RegisterModule(Action<CCSPlayerController, CCSPlayerController, string> action);
        bool HasRegisteredModules();
        void StartQuiz(int quizMode);
        int GetQuizValue();
        string GetAnswer();
        void SetAnswer(string answer);
        void TriggerPlayerWin(CCSPlayerController player);
        void HandleClientAnswer(CCSPlayerController player, string answer);
        string GetTranslatedText(string name, params object[] args);

        event Action? ModuleRegistered;
        event Action<CCSPlayerController> OnPlayerWin;
        event Action OnQuizStart;
        event Action OnQuizEnd;
        event Action<CCSPlayerController> OnClientAnswered;
        event Action<CCSPlayerController> OnPlayerLose;

        Dictionary<CCSPlayerController, Action<string>>? NextCommandAction { get; set; }
    }
}
