using Azure.AI.Agents.Persistent;
using Azure.Identity;
using FormExtractionApi.Models;
using System.Text;

namespace FormExtractionApi.Services;

public class AgentCaller
{
    private readonly PersistentAgentsClient _agentsClient;
    private readonly PersistentAgent _agent;

    public AgentCaller(string endpointUrl)
    {
        // Use DefaultAzureCredential or another credential type
        var credential = new DefaultAzureCredential();
        _agentsClient = new PersistentAgentsClient(endpointUrl, credential);
        //_agent = _agentsClient.Administration.GetAgent("asst_6JOFuh8a2BSbRJO86veWd5l8");
    }

    // Text input
    public async Task<string> CallAgentAsync(string agentId, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId, nameof(agentId));
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));

        var agent = _agentsClient.Administration.GetAgent(agentId);
        var thread = _agentsClient.Threads.CreateThread();

        _agentsClient.Messages.CreateMessage(thread.Value.Id, MessageRole.User, text);

        var runResponse = _agentsClient.Runs.CreateRun(thread.Value.Id, agent.Value.Id);
        await PollRunCompletionAsync(thread.Value.Id, runResponse.Value.Id);

        return await GetRunResultAsync(thread.Value.Id);
    }

    // Overload for image input
    public async Task<string> CallAgentAsync(string agentId, string text, Stream imageStream, string imageFileName, string imageContentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId, nameof(agentId));
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));
        ArgumentNullException.ThrowIfNull(imageStream, nameof(imageStream));
        ArgumentException.ThrowIfNullOrEmpty(imageFileName, nameof(imageFileName));
        ArgumentException.ThrowIfNullOrEmpty(imageContentType, nameof(imageContentType));

        if (!imageStream.CanRead)
        {
            throw new ArgumentException("Image stream must be readable", nameof(imageStream));
        }

        var agent = _agentsClient.Administration.GetAgent(agentId);
        var thread = _agentsClient.Threads.CreateThread();

        // Upload the image file
        var fileUpload = _agentsClient.Files.UploadFile(imageStream, imageFileName, imageContentType);

        // Create an image attachment using the constructor
        var imageAttachment = new MessageAttachment(
            fileUpload.Value.Id,
            new List<ToolDefinition>()  // Empty list of tools since we're just attaching an image
        );

        // Create the message with text and image attachment
        _agentsClient.Messages.CreateMessage(
            thread.Value.Id,
            MessageRole.User,
            text,
            new[] { imageAttachment }
        );

        var runResponse = _agentsClient.Runs.CreateRun(thread.Value.Id, agent.Value.Id);
        await PollRunCompletionAsync(thread.Value.Id, runResponse.Value.Id);

        return await GetRunResultAsync(thread.Value.Id);
    }

    // Polling helper
    private async Task<ThreadRun> PollRunCompletionAsync(string threadId, string runId)
    {
        ThreadRun run;
        do
        {
            await Task.Delay(500);
            run = _agentsClient.Runs.GetRun(threadId, runId);
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

        if (run.Status != RunStatus.Completed)
        {
            throw new InvalidOperationException($"Run failed or was canceled: {run.LastError?.Message}");
        }

        return run;
    }

    // Result retrieval helper
    private async Task<string> GetRunResultAsync(string threadId)
    {
        var messages = _agentsClient.Messages.GetMessages(threadId, order: ListSortOrder.Ascending);
        var sb = new StringBuilder();

        foreach (var msg in messages)
        {
            foreach (var content in msg.ContentItems)
            {
                switch (content)
                {
                    case MessageTextContent text:
                        sb.AppendLine(text.Text);
                        break;
                    case MessageImageFileContent image:
                        sb.AppendLine($"<image: {image.FileId}>");
                        break;
                    //case MessageFileContent file:
                    //    sb.AppendLine($"<file: {file.FileId}>");
                    //    break;
                }
            }
        }

        return sb.ToString();
    }
}

