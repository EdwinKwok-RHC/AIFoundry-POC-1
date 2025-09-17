
# AIFoundry-POC-1
AIFoundry Multi-Agent POC demostrate how to use Agent in Azure AI Foundry. This also show how to call multiple agents


## 🧱 Step 1: Create the Project Using .NET CLI

    dotnet new sln -n FormExtractionSolution
    dotnet new webapi -n FormExtractionApi --use-minimal-apis
    dotnet sln FormExtractionSolution.sln add FormExtractionApi/FormExtractionApi.csproj

## 📦 Step 2: Add Required NuGet Packages

    cd FormExtractionApi
    dotnet add package Azure.AI.Agents
    dotnet add package Azure.Identity
    dotnet add package Swashbuckle.AspNetCore
    dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer


## 🧱 Step 3: Create the `FormExtractionAgent` Enum

Create a new file: `Models/FormExtractionAgent.cs`

    namespace FormExtractionApi.Models;
    public enum FormExtractionAgent
    {
    	FormManagerAgent,
    	WaterHeaterRepairFormAgent,
    	HVACFormAgent,
    	PlumbingFormAgent,
    	GenericFormAgent,
    	OtherAgent
    }

## 🧱 Step 4: Add the `AgentCaller` Class

Create a new file: `Services/AgentCaller.cs`

Paste the full implementation from earlier (using Azure SDK), and make sure it’s in the `FormExtractionApi.Services` namespace.

## 🧱 Step 5: Register `AgentCaller` in DI

Update `Program.cs`:

    using FormExtractionApi.Services;
    
    var builder = WebApplication.CreateBuilder(args);
    
    // Register AgentCaller with endpoint from config
    builder.Services.AddSingleton(sp =>
    {
        var endpoint = builder.Configuration["AzureFoundry:Endpoint"];
        return new AgentCaller(endpoint);
    });
    
    var app = builder.Build();

Add this to `appsettings.json`:

    {
      "AzureFoundry": {
        "Endpoint": "https://your-resource.services.ai.azure.com/api/projects/your-project-id"
      }
    }

## 🧱 Step 6: Add `/FormExtraction` Endpoint

In `Program.cs`, below `app.MapGet(...)`, add:

    using FormExtractionApi.Models;
    
    app.MapPost("/FormExtraction", async (HttpRequest request, AgentCaller caller) =>
    {
        if (!request.HasFormContentType)
            return Results.BadRequest("Form content type required.");
    
        var form = await request.ReadFormAsync();
        var file = form.Files["image"];
        if (file == null || file.Length == 0)
            return Results.BadRequest("Image file is missing.");
    
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var imageBytes = ms.ToArray();
    
        // Step 1: Get form type from FormManagerAgent
        var formTypeResponse = await caller.CallAgentAsync(FormExtractionAgent.FormManagerAgent, "Please extract this");
        var formType = formTypeResponse.Trim().ToLowerInvariant();
    
        // Step 2: Route to correct agent
        FormExtractionAgent targetAgent = formType switch
        {
            var s when s.Contains("water heater") => FormExtractionAgent.WaterHeaterRepairFormAgent,
            var s when s.Contains("hvac") => FormExtractionAgent.HVACFormAgent,
            var s when s.Contains("plumbing") => FormExtractionAgent.PlumbingFormAgent,
            _ => FormExtractionAgent.GenericFormAgent
        };
    
        var extractedData = await caller.CallAgentAsync(targetAgent, imageBytes);
        return Results.Ok(new { FormType = formType, Data = extractedData });
    });

## 🧪 Step 7: Run and Test

    dotnet run
Use Postman or curl to POST an image:

    curl -X POST https://localhost:5001/FormExtraction \  -F "image=@form.jpg"
