using System;
using System.Threading.Tasks;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Server.Interfaces;

namespace Server;

public class ServiceImpl : ServiceOne, ServiceTwo, IService
{
	private readonly CurrentUser currentUser;
	private readonly ILogger<ServiceImpl> logger;

	public ServiceImpl(CurrentUser currentUser, ILogger<ServiceImpl> logger)
	{
		this.currentUser = currentUser;
		this.logger = logger;
	}
	public async Task<Response<MethodResponseOne>> MethodOne(MethodRequestOne request)
	{
		if (request.Text.Contains("next"))
		{
			return new Error(ErrorCode.InvalidInput, "Next is forbidden");
		}
		await Task.CompletedTask;

		logger.LogInformation("Method one called with request: " + request.Text + ". " + currentUser.Token);
		return new MethodResponseOne { Text = request.Text };
	}

	public Task<Response<MethodResponseTwo>> MethodTwo(MethodRequestTwo request)
	{
		Response<MethodResponseTwo> response;
		if (request.Text.Contains("next"))
		{
			response = new Error(ErrorCode.InvalidInput, "Next is forbidden");
			logger.LogError(response.Error.Description);
			return Task.FromResult(response);
		}

		logger.LogInformation("Method two called with request: " + request.Text);
		response = new MethodResponseTwo { Text = request.Text };
		return Task.FromResult(response);
	}


    [Authorize]
    public Task<Response<MethodResponseThree>> MethodThree(MethodRequestThree request)
	{
		Response<MethodResponseThree> response;
		if (request.Text.Contains("next"))
		{
			response = new Error(ErrorCode.InvalidInput, "Next is forbidden");
			return Task.FromResult(response);
		}

		logger.LogInformation("Method three called with request: " + request.Text);
		response = new MethodResponseThree { Text = request.Text };
		return Task.FromResult(response);
	}

	public async Task<Response<MethodResponseOne>> UploadFile(FileTestRequest request)
	{
		logger.LogInformation($"Saving file {request.Name} with id {request.Id}");
		using var file = System.IO.File.Create($"{request.Name}.txt");
		await request.File.CopyToAsync(file);
		file.Flush();
		file.Close();
		return new Response<MethodResponseOne>(new MethodResponseOne { Text = "File saved" });
	}

}
