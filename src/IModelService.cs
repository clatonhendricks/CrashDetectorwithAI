using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CrashDetectorwithAI
{
    public interface IModelService : IDisposable
    {
        bool IsInitialized { get; }
        Task InitializeAsync();
        IAsyncEnumerable<string> GenerateResponseStreamingAsync(string systemPrompt, string userPrompt);
    }
}
