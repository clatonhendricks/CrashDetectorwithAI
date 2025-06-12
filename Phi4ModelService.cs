using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntimeGenAI;
using System.Linq;
using System.Windows;

namespace CrashDetectorwithAI
{
    public class Phi4ModelService : IDisposable
    {
        private string modelPath = string.Empty;
        private Model? model = null;
        private Tokenizer? tokenizer = null;
        private bool isInitialized = false;
        private Action<string> logAction;

        public Phi4ModelService(string modelPath, Action<string> logAction)
        {
            this.modelPath = modelPath;
            this.logAction = logAction ?? (s => { }); 
        }

        public bool IsInitialized => isInitialized && model != null && tokenizer != null;

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Log start of model loading
                    logAction("Initializing model...");

                    // Start timing
                    var sw = Stopwatch.StartNew();

                    if (string.IsNullOrEmpty(modelPath) || !Directory.Exists(modelPath))
                    {
                        throw new DirectoryNotFoundException($"Model directory not found: {modelPath}");
                    }

                    // Find the ONNX model file directory 
                    logAction($"Loading model from: {modelPath}");

                    // Initialize model using the correct API
                    model = new Model(modelPath);
                    tokenizer = new Tokenizer(model);
                    isInitialized = true;
                    
                    sw.Stop();
                   
                    logAction($"Model loaded in {sw.ElapsedMilliseconds} ms");
                }
                catch (Exception ex)
                {
                    logAction($"Failed to load model: {ex.Message}");
                    throw; 
                }
            });
        }

        public async Task<string> GenerateResponseAsync(string prompt)
        {
            return await Task.Run(() =>
            {
                if (model == null || tokenizer == null)
                {
                    throw new InvalidOperationException("Model not initialized. Call InitializeAsync first.");
                }

                try
                {
                    StringBuilder output = new StringBuilder();
                    
                    var tokens = tokenizer.Encode(prompt);
                    
                    
                    var generatorParams = new GeneratorParams(model);
                    generatorParams.SetSearchOption("max_length", 2048);
                    generatorParams.SetSearchOption("past_present_share_buffer", false);
                    
                    
                    using var tokenizerStream = tokenizer.CreateStream();
                    
                   
                    using var generator = new Generator(model, generatorParams);
                    
                    
                    generator.AppendTokens(tokens[0].ToArray());
                    
                    
                    logAction("Generating response...");
                    
                    // Generate tokens one by one until done
                    while (!generator.IsDone())
                    {
                        generator.GenerateNextToken();
                        string tokenText = tokenizerStream.Decode(generator.GetSequence(0)[^1]);
                        output.Append(tokenText);
                        
                        // Check for end tokens
                        if (tokenText.Contains("<|end|>"))
                        {
                            break;
                        }
                    }
                    
                    return output.ToString();
                }
                catch (Exception ex)
                {
                    logAction($"Inference error: {ex.Message}");
                    throw; 
                }
            });
        }

        public void Dispose()
        {
            model?.Dispose();
            model = null;
            tokenizer = null;
            isInitialized = false;
        }

       
        public static string FormatPrompt(string systemPrompt, string userPrompt)
        {
            return $"<|system|>{systemPrompt}<|end|><|user|>{userPrompt}<|end|><|assistant|>";
        }
    }
}
