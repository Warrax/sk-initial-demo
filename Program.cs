using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.KernelMemory;
using SemanticKernelPoC.Plugins;
using Microsoft.SemanticKernel.Plugins.Core;


var modelId = "qwen/qwen3-1.7b";
var apiUrl = @"http://127.0.0.1:1234/v1/";


var builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(
modelId: modelId,
apiKey: modelId,
endpoint: new Uri(apiUrl));

//builder.Services.AddAzureOpenAIChatCompletion(
//    deploymentName: builder.Configuration["AzureOpenAI:DeploymentName"],
//    endpoint: builder.Configuration["AzureOpenAI:Endpoint"],
//    apiKey: builder.Configuration["AzureOpenAI:ApiKey"]
//);
//builder.AddAzureOpenAIChatCompletion(
//    deploymentName: "NAME_OF_YOUR_DEPLOYMENT",
//    apiKey: "YOUR_API_KEY",
//    endpoint: "YOUR_AZURE_ENDPOINT",
//    modelId: "gpt-4", // Optional name of the underlying model if the deployment name doesn't match the model name
//    serviceId: "YOUR_SERVICE_ID", // Optional; for targeting specific services within Semantic Kernel
//    httpClient: new HttpClient() // Optional; if not provided, the HttpClient from the kernel will be used
//);

builder.Plugins.AddFromType<DummyDbPlugin>();
builder.Plugins.AddFromType<TimePlugin>();

Kernel kernel = builder.Build();

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    Temperature = 0.1,
    ChatSystemPrompt = File.ReadAllText(".\\SystemPrompt.txt")
};

// store the conversation
var history = new ChatHistory();


while(true) { 

    Console.Write("User > ");

    string? userPrompt = Console.ReadLine();

    if (userPrompt is null)
        break;

    history.AddUserMessage(userPrompt);

    var result = await chatCompletionService.GetChatMessageContentAsync(
        history,
        executionSettings: openAIPromptExecutionSettings,
        kernel: kernel);

    Console.WriteLine("Assistant > " + result);

    history.AddMessage(result.Role, result.Content ?? string.Empty);
}

/*
 
User -> LLM: Задаёт вопрос
LLM -> LLM: Получает system prompt, tools definitions (MCP schema), историю диалога
LLM -> LLM: Анализирует вопрос
alt Нужно использовать инструмент
    LLM -> MCP-tool: get_client_transactions(startDate, endDate, limit)
    MCP-tool -> LLM: Возвращает { meta, transactions }
    LLM -> LLM: Интерпретирует результат согласно system prompt
    alt transactions пуст
        LLM -> User: "В предоставленных данных нет информации для ответа на этот вопрос."
    else
        LLM -> User: Формирует ответ (фактический вывод + данные)
    end
else Можно ответить без инструмента
    LLM -> User: Формирует ответ напрямую
end
LLM -> LLM: Обновляет историю диалога (вопросы, ответы, результаты инструментов)

 */
