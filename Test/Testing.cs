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
			.Next(async (res) =>
			{
				var res2 = await sp.Create<ServiceTwo>("Server", "ServiceImpl").MethodTwo(new MethodRequestTwo { Text = "Hello " });

				var x = new Response<(Empty first, MethodResponseTwo second)>((res, res2.Result!));
				return x;
			})
			// Create failure request
			.Next(async (res) =>
			{
				var a = await sp.Create<ServiceTwo>("Server", "ServiceImpl").MethodThree(new MethodRequestThree { Text = res.first.ToString() + " there! nex" });
				if (a.HasError)
				{
					Console.WriteLine("ERROR!!! " + a.Error.Description);
					return new Response<(Empty First, MethodResponseTwo Second, MethodResponseThree Third)>(a.Error);
				}
				var y = new Response<(Empty First, MethodResponseTwo Second, MethodResponseThree Third)>((res.first, res.second, a.Result!));
				return y;
			})
			.OnError((error) =>
			{
				Console.WriteLine("ERROR!!! " + error.Description);
				return Task.FromResult(new Response<(Empty First, MethodResponseTwo Second, MethodResponseThree Third)>(error));
			});

		if (ress.HasError)
		{
			Console.WriteLine("Returned error " + ress.Error.Description);
			return ress.Error.Description;
		}
		else
		{
			Console.WriteLine("Returned " + ress.Result.Third.Text);
			return ress.Result.Third.Text;
		}

	}
}