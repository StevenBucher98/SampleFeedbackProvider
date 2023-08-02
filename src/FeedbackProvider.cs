using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Feedback;
using System.Management.Automation.Subsystem.Prediction;
using System.Management.Automation.Language;

namespace SampleFeedbackProvider;

public sealed class SampleFeedbackProvider : IFeedbackProvider, ICommandPredictor
{
    private readonly Guid _guid;

    // this is the list of actions from the feedback provider that will be sent to the predictor 
    private List<string>? _candidates;
    
    // implement the interface member to get trigger to work
    public FeedbackTrigger Trigger => FeedbackTrigger.All;
    
    internal SampleFeedbackProvider(string guid)
    {
        _guid = new Guid(guid);
    }

    Dictionary<string, string>? ISubsystem.FunctionsToDefine => null;

    public Guid Id => _guid;

    public string Name => "sample-feedback-provider";

    public string Description => "This is a simple feedback provider to demonstrate the feedback subsystem.";

    #region IFeedbackProvider

    public FeedbackItem? GetFeedback(FeedbackContext context, CancellationToken token)
    {
       
        var target = context.Trigger;
        var commandLine = context.CommandLine;
        
        // check if commandline is empty
        if (string.IsNullOrEmpty(commandLine))
        {
            return null;
        }

        PowerShell powershell = PowerShell.Create().AddCommand("Get-Alias").AddParameter("Name", commandLine);

        var results = powershell.Invoke();


        if (target == FeedbackTrigger.Success)
        {
            string header = "This is an aliased command in PowerShell, this is the fully qualified command";
            List<string>? actions = new List<string>();
            //actions.Add((string)result.Name);

            if (results.Count > 0)
            {
                // foreach(PSObject result in results){
                //     actions.Add(result.Members["ReferencedCommand"].Value.ToString());

                // }
                actions.Add(results[0].Members["ReferencedCommand"].Name.ToString());
                return new FeedbackItem(header, actions);
            }
            else
            {
                actions.Add("No alias found");
                return new FeedbackItem(header, actions);

            }
            // foreach (var result in results) {
            //     actions.Add(result.Members["DisplayName"].Value.ToString());
            // }
            
            _candidates = actions;
            return null;
        }

        

        // if (target == FeedbackTrigger.Error)
        // {
        //     string header = "This is the error header";
        //     List<string>? actions = new List<string>();
        //     actions.Add("Error1");
        //     actions.Add("Error2");
        //     return new FeedbackItem(header, actions);
        // }

        if (target == FeedbackTrigger.Comment)
        {
            string header = "This is the header";
            List<string>? actions = new List<string>();
            actions.Add("Action1");
            actions.Add((string)commandLine);
            //actions.Add(result);
            string footer = "This is the footer";
            _candidates = actions;
            return new FeedbackItem(header, actions, footer, FeedbackDisplayLayout.Portrait);
        }
        

        // // Use the different trigger 'CommandNotFound', so 'LastError' won't be null.
        // var target = (string)context.LastError!.TargetObject;
        // if (target is null || target.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        // {
        //     return null;
        // }

        // string? cmd_not_found = GetUtilityPath();
        // if (cmd_not_found is not null)
        // {
        //     var startInfo = new ProcessStartInfo(cmd_not_found);
        //     startInfo.ArgumentList.Add(target);
        //     startInfo.RedirectStandardError = true;
        //     startInfo.RedirectStandardOutput = true;

        //     using var process = Process.Start(startInfo);
        //     if (process is not null)
        //     {
        //         string? header = null;
        //         List<string>? actions = null;

        //         while (true)
        //         {
        //             string? line = process.StandardError.ReadLine();
        //             if (line is null)
        //             {
        //                 break;
        //             }

        //             if (line == string.Empty)
        //             {
        //                 continue;
        //             }

        //             if (line.StartsWith("sudo ", StringComparison.Ordinal))
        //             {
        //                 actions ??= new List<string>();
        //                 actions.Add(line.TrimEnd());
        //             }
        //             else if (actions is null)
        //             {
        //                 header = line;
        //             }
        //         }

        //         if (actions is not null && header is not null)
        //         {
        //             _candidates = actions;

        //             var footer = process.StandardOutput.ReadToEnd().Trim();
        //             return string.IsNullOrEmpty(footer)
        //                 ? new FeedbackItem(header, actions)
        //                 : new FeedbackItem(header, actions, footer, FeedbackDisplayLayout.Portrait);
        //         }
        //     }
        // }

        return null;
    }

    #endregion

    #region ICommandPredictor

    public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback)
    {
        return feedback switch
        {
            PredictorFeedbackKind.CommandLineAccepted => true,
            _ => false,
        };
    }

    public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
    {
        if (_candidates is not null)
        {
            string input = context.InputAst.Extent.Text;
            List<PredictiveSuggestion>? result = null;

            foreach (string c in _candidates)
            {
                if (c.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                {
                    result ??= new List<PredictiveSuggestion>(_candidates.Count);
                    result.Add(new PredictiveSuggestion(c));
                }
            }

            if (result is not null)
            {
                return new SuggestionPackage(result);
            }
        }

        return default;
    }

    public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history)
    {
        // Reset the candidate state.
        _candidates = null;
    }

    public void OnSuggestionDisplayed(PredictionClient client, uint session, int countOrIndex) { }

    public void OnSuggestionAccepted(PredictionClient client, uint session, string acceptedSuggestion) { }

    public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success) { }

    #endregion;
}

public class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    private const string Id = "47013747-CB9D-4EBC-9F02-F32B8AB19D48";

    public void OnImport()
    {
        var feedback = new SampleFeedbackProvider(Id);
        SubsystemManager.RegisterSubsystem(SubsystemKind.FeedbackProvider, feedback);
        SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, feedback);
    }

    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        SubsystemManager.UnregisterSubsystem<ICommandPredictor>(new Guid(Id));
        SubsystemManager.UnregisterSubsystem<IFeedbackProvider>(new Guid(Id));
    }
}
