using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CrashDetectorwithAI
{
    public class OllamaModelService : IModelService
    {
        private readonly string endpoint;
        private readonly string modelName;
        private readonly Action<string> logAction;
        private readonly HttpClient httpClient;
        private bool isInitialized;

        public OllamaModelService(string endpoint, string modelName, Action<string> logAction)
        {
            this.endpoint = endpoint.TrimEnd('/');
            this.modelName = modelName;
            this.logAction = logAction ?? (s => { });
            this.httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        public bool IsInitialized => isInitialized;

        public async Task InitializeAsync()
        {
            try
            {
                logAction("Connecting to Ollama...");

                var response = await httpClient.GetAsync($"{endpoint}/api/tags");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var models = doc.RootElement.GetProperty("models");

                bool modelFound = false;
                foreach (var model in models.EnumerateArray())
                {
                    var name = model.GetProperty("name").GetString();
                    if (name != null && (name == modelName || name.StartsWith($"{modelName}:")))
                    {
                        modelFound = true;
                        break;
                    }
                }

                if (!modelFound)
                {
                    logAction($"Warning: Model '{modelName}' not found locally. Ollama may need to pull it on first use.");
                    logAction($"You can pre-download it with: ollama pull {modelName}");
                }
                else
                {
                    logAction($"Model '{modelName}' is available.");
                }

                isInitialized = true;
                logAction("Ollama connection established.");
            }
            catch (HttpRequestException ex)
            {
                logAction($"Failed to connect to Ollama at {endpoint}: {ex.Message}");
                logAction("Make sure Ollama is running (ollama serve).");
                throw;
            }
            catch (Exception ex)
            {
                logAction($"Failed to initialize Ollama: {ex.Message}");
                throw;
            }
        }

        public async IAsyncEnumerable<string> GenerateResponseStreamingAsync(string systemPrompt, string userPrompt)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync first.");
            }

            var requestBody = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                stream = true
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var channel = Channel.CreateUnbounded<string>();

            var streamTask = Task.Run(async () =>
            {
                try
                {
                    logAction("Generating response...");

                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/api/chat")
                    {
                        Content = httpContent
                    };

                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var reader = new StreamReader(stream);

                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line)) continue;

                        try
                        {
                            using var jsonDoc = JsonDocument.Parse(line);
                            var root = jsonDoc.RootElement;

                            if (root.TryGetProperty("message", out var message) &&
                                message.TryGetProperty("content", out var content))
                            {
                                var token = content.GetString();
                                if (!string.IsNullOrEmpty(token))
                                {
                                    channel.Writer.TryWrite(token);
                                }
                            }

                            if (root.TryGetProperty("done", out var done) && done.GetBoolean())
                            {
                                break;
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip malformed JSON lines
                        }
                    }
                }
                catch (Exception ex)
                {
                    logAction($"Ollama error: {ex.Message}");
                    channel.Writer.Complete(ex);
                    return;
                }

                channel.Writer.Complete();
            });

            await foreach (var token in channel.Reader.ReadAllAsync())
            {
                yield return token;
            }

            await streamTask;
        }

        public void Dispose()
        {
            httpClient?.Dispose();
            isInitialized = false;
        }
    }
}
