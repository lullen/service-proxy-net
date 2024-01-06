using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Luizio.ServiceProxy.Client;
using Luizio.ServiceProxy.Models;
using Server.Interfaces;
namespace Test;



public class Testing
{
	private readonly IProxy sp;
	private readonly CurrentUser currentUser;

	public Testing(IProxy sp, CurrentUser currentUser)
	{
		this.sp = sp;
		this.currentUser = currentUser;
	}
	public async Task<string> Run()
	{
		if (!currentUser.Metadata.Any())
		{
			currentUser.Metadata.Add(KeyValuePair.Create("Test", "Testing"));
			currentUser.Metadata.Add(KeyValuePair.Create("Authorization", "Bearer sdgsdf3245236"));
		}


		var ress = await sp.Create<ServiceOne>("Server", "ServiceImpl").MethodOne(new MethodRequestOne { Text = "Hi there!" })
			.Next((res) => sp.Create<ServiceTwo>("Server", "ServiceImpl").MethodTwo(new MethodRequestTwo { Text = res.Text + " Hello " }))
			// Create failure request
			.Next((res) => sp.Create<ServiceTwo>("Server", "ServiceImpl").MethodThree(new MethodRequestThree { Text = res.Text + " there! next" }))
			.OnError((error) =>
			{
				Console.WriteLine("ERROR!!! " + error.Description);
				return Task.FromResult(new Response<MethodResponseThree>(error));
			});

		if (ress.HasError)
		{
			Console.WriteLine("Returned error " + ress.Error.Description);
			return ress.Error.Description;
		}
		else
		{
			Console.WriteLine("Returned " + ress.Result.Text);
			return ress.Result.Text;
		}

	}
}