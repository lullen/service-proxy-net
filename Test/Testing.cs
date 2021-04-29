using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Proxy;
using Proxy.NewProxy;
using Test.Interfaces;

namespace Test
{
    public class ImplService : ServiceOne
    {
        public Task<Response<MethodClass>> MethodOne(MethodClass request)
        {
            if (request.Text.Contains("next"))
            {
                return Task.FromResult(new Response<MethodClass>(new Error(ErrorCode.InvalidInput, "Next is forbidden")));
            }

            Console.WriteLine("Method one called with request: " + request.Text);
            return Task.FromResult(new Response<MethodClass>(new MethodClass { Text = request.Text }));
        }
    }

    public class MethodClass : IMessage
    {
        public string Text { get; set; }

        public MessageDescriptor Descriptor => throw new NotImplementedException();

        public int CalculateSize()
        {
            throw new NotImplementedException();
        }

        public void MergeFrom(CodedInputStream input)
        {
            throw new NotImplementedException();
        }

        public void WriteTo(CodedOutputStream output)
        {
            throw new NotImplementedException();
        }
    }

    public class Testing
    {
        private readonly ServiceProxy sp;

        public Testing(ServiceProxy sp)
        {
            this.sp = sp;
        }
        public async Task Run()
        {
            var a = sp.Create<ServiceOne>();


            var ress2 = await a.MethodOne(new MethodClass { Text = "Hi there!" });
            if (ress2.HasError)
            {
                Console.WriteLine("Returned error " + ress2.Error.Description);
            }
            var ress22 = await a.MethodOne(new MethodClass { Text = ress2.Result.Text + " Hello!" });
            if (ress2.HasError)
            {
                Console.WriteLine("Returned error " + ress22.Error.Description);
            }
            
            var ress222 = await a.MethodOne(new MethodClass { Text = ress22.Result.Text + " Hello!" });
            if (ress222.HasError)
            {
                Console.WriteLine("Returned error " + ress222.Error.Description);
            }

            var ress = await a.MethodOne(new MethodClass { Text = "Hi there!" })
                .Next((res) => a.MethodOne(new MethodClass { Text = res.Text + " Hello!" }))
                .Next((res) => a.MethodOne(new MethodClass { Text = res.Text + " next2" }))
                .OnError((error) =>
                {
                    // Error handling
                    return Task.FromResult(new Response<MethodClass>(error));
                });

            if (ress.HasError)
            {
                Console.WriteLine("Returned error " + ress.Error.Description);
            }
            else
            {
                Console.WriteLine("Returned " + ress.Result.Text);
            }

            // .Next((res) => a.MethodOne(new MethodClass { Text = res.Text + " next" }));


        }
    }
}