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
            this.logAction = logAction ?? (s => { }); // Default no-op if no logging action provided
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

                    // Find the ONNX model file directory (don't need file path directly with this API)
                    logAction($"Loading model from: {modelPath}");

                    // Initialize model using the correct API
                    model = new Model(modelPath);
                    tokenizer = new Tokenizer(model);
                    isInitialized = true;

                    // Stop timing
                    sw.Stop();

                    // Log completion
                    logAction($"Model loaded in {sw.ElapsedMilliseconds} ms");
                }
                catch (Exception ex)
                {
                    logAction($"Failed to load model: {ex.Message}");
                    throw; // Re-throw to let caller handle the error
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
                    // Create a StringBuilder to collect the output
                    StringBuilder output = new StringBuilder();
                    
                    // Encode the input prompt
                    var tokens = tokenizer.Encode(prompt);
                    
                    // Set up the generator parameters
                    var generatorParams = new GeneratorParams(model);
                    generatorParams.SetSearchOption("max_length", 2048);
                    generatorParams.SetSearchOption("past_present_share_buffer", false);
                    
                    // Create tokenizer stream for decoding
                    using var tokenizerStream = tokenizer.CreateStream();
                    
                    // Create the generator
                    using var generator = new Generator(model, generatorParams);
                    
                    // Append input tokens to the generator
                    generator.AppendTokens(tokens[0].ToArray());
                    
                    // Log the token generation start
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
                    throw; // Re-throw to let caller handle the error
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

        // Helper method to format a prompt with system and user messages
        public static string FormatPrompt(string systemPrompt, string userPrompt)
        {
            return $"<|system|>{systemPrompt}<|end|><|user|>{userPrompt}<|end|><|assistant|>";
        }
    }
}