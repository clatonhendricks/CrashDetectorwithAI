[![Build](https://github.com/clatonhendricks/CrashDetectorwithAI/actions/workflows/build.yml/badge.svg)](https://github.com/clatonhendricks/CrashDetectorwithAI/actions/workflows/build.yml)
 
 # CrashDetectorwithAI

A Windows application that detects system crashes and uses AI to provide explanations and solutions for crash events. Supports multiple AI backends including local Phi-4 (ONNX) and Ollama for access to a wide range of local models.

## Features

- Automatically detects Windows crash events
- Extracts fault bucket information from Windows Error Reporting
- **Multiple AI service support**: Phi-4 (local ONNX) and Ollama
- Streaming AI responses with real-time token display
- Modern, sleek UI with responsive design

| Detection| AI recommendation |
|----------|----------|
| <img width="408" height="223" alt="Image" src="https://github.com/user-attachments/assets/64aa0f09-d066-4de7-938e-866937ee1220" />   | <img width="431" height="511" alt="Image" src="https://github.com/user-attachments/assets/54f87c72-15e2-4e33-855e-fcce878aedb9" />   |


## Technical Details

- Built with WPF (.NET 9.0)
- Uses ONNX Runtime for local Phi-4 model inference
- Supports Ollama REST API for access to additional local models
- `IModelService` interface enables extensible AI service integration

## Requirements

- Windows operating system
- .NET 9.0 runtime
- One of the following AI backends:
  - **Phi-4**: Model files in ONNX format (configured in appSettings.yml)
  - **Ollama**: [Ollama](https://ollama.com/) installed and running

## Setup

1. Clone the repository
2. Update the `appSettings.yml` with your preferred AI service configuration
3. Build and run the application

## AI Services

### Phi-4 (Local ONNX) — Default

Uses the Phi-4 model locally via ONNX Runtime. Best for offline use with no additional services.

1. Download the Phi-4 ONNX model (Int4 quantized, CPU-optimized)
2. Set `modelPath` in `appSettings.yml` to the model directory
3. Select **Phi-4 (Local)** in the app

### Ollama

Supports any model available through [Ollama](https://ollama.com/), such as Llama 3, Mistral, DeepSeek, Phi-4, Gemma, and more.

1. Install Ollama from https://ollama.com/
2. Pull a model: `ollama pull llama3.2`
3. Ensure Ollama is running: `ollama serve`
4. Update `appSettings.yml`:
   ```yaml
   aiService: ollama
   ollamaEndpoint: http://localhost:11434
   ollamaModel: llama3.2
   ```
5. Select **Ollama** in the app and enter your model name

## Configuration

In `appSettings.yml`, configure your AI service:

```yaml
# AI Service selection: phi4 or ollama
aiService: phi4

# Path to the Phi-4 model (used when aiService is phi4)
modelPath: C:\Path\To\Your\Phi4Model

# Ollama configuration (used when aiService is ollama)
ollamaEndpoint: http://localhost:11434
ollamaModel: llama3.2
```

YAML configuration allows you to use regular Windows paths with backslashes without needing to escape them.

