using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SignalR.Client;

namespace crank
{
    class Program
    {
        private static bool _running;

        static void Main(string[] args)
        {
            Console.WriteLine("Crank v{0}", typeof(Program).Assembly.GetName().Version);
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: crank [url] [numclients] <batchSize> <batchInterval>");
                return;
            }

            ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;
            _running = true;

            string url = args[0];
            int clients = Int32.Parse(args[1]);
            int batchSize = args.Length < 3 ? 50 : Int32.Parse(args[2]);
            int batchInterval = args.Length < 4 ? 3000 : Int32.Parse(args[3]);

            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            var connections = new ConcurrentBag<Connection>();

            var sw = Stopwatch.StartNew();

            Task.Factory.StartNew(() =>
            {
                Console.WriteLine("Ramping up connections. Batch size {0}.", batchSize);

                var rampupSw = Stopwatch.StartNew();
                ConnectBatches(url, clients, batchSize, batchInterval, connections).ContinueWith(task =>
                {
                    Console.WriteLine("Started {0} connection(s).", connections.Count);

                    Console.WriteLine("Setting up event handlers");

                    rampupSw.Stop();
                    Console.WriteLine("Ramp up complete in {0}.", rampupSw.Elapsed);
                });
            });

            Console.WriteLine("Press any key to stop running...");
            Console.Read();
            sw.Stop();
            _running = false;

            Console.WriteLine("Total Running time: {0}", sw.Elapsed);
            Console.WriteLine("End point: {0}", url);
            Console.WriteLine("Total connections: {0}", clients);
            Console.WriteLine("Active connections: {0}", connections.Count(c => c.IsActive));
            Console.WriteLine("Stopped connections: {0}", connections.Count(c => !c.IsActive));

            Console.WriteLine("Closing connection(s).");
            foreach (var connection in connections)
            {
                connection.Stop();
            }
        }

        private static Task ConnectBatches(string url, int clients, int batchSize, int batchInterval, ConcurrentBag<Connection> connections)
        {
            int processed = Math.Min(clients, batchSize);

            var sw = Stopwatch.StartNew();
            Console.WriteLine("Remaining clients {0}", clients);
            return ConnectBatch(url, processed, connections).ContinueWith(t =>
            {
                sw.Stop();
                Console.WriteLine("Batch took {0}", sw.Elapsed);

                int remaining = clients - processed;

                if (remaining > 0)
                {
                    // Give this batch a few seconds to connect
                    Thread.Sleep(batchInterval);

                    return ConnectBatches(url, remaining, batchSize, batchInterval, connections);
                }

                var tcs = new TaskCompletionSource<object>();
                tcs.TrySetResult(null);
                return tcs.Task;
            })
            .Unwrap();
        }

        private static Task ConnectBatch(string url, int batchSize, ConcurrentBag<Connection> connections)
        {
            var tcs = new TaskCompletionSource<object>();
            long remaining = batchSize;
            Parallel.For(0, batchSize, i =>
            {
                var connection = new Connection(url);

                connection.Start().ContinueWith(task =>
                {
                    remaining = Interlocked.Decrement(ref remaining);

                    if (task.IsFaulted)
                    {
                        Console.WriteLine("Failed to start client. {0}", task.Exception.GetBaseException());
                    }
                    else
                    {
                        connections.Add(connection);

                        var clientId = connection.ClientId;

                        //connection.Received += data =>
                        //{
                        //    Console.WriteLine("Client {0} RECEIVED: {1}", clientId, data);
                        //};

                        connection.Error += e =>
                        {
                            Debug.WriteLine(String.Format("SIGNALR: Client {0} ERROR: {1}", clientId, e));
                        };

                        connection.Closed += () =>
                        {
                            Debug.WriteLine(String.Format("SIGNALR: Client {0} CLOSED", clientId));
                        };

                    }

                    if (Interlocked.Read(ref remaining) == 0)
                    {
                        // When all connections are connected, mark the task as complete
                        tcs.TrySetResult(null);
                    }
                });
            });

            return tcs.Task;
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception.GetBaseException());
            e.SetObserved();
        }
    }
}
