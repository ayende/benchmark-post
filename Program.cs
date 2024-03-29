﻿using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Bench.BulkInsert
{
    class Program
    {
        static void Main(string[] args)
        {
            var addressGen = new Bogus.Faker<Address>()
                .StrictMode(true)
                .RuleFor(x => x.Line1, x => x.Address.StreetAddress())
                .RuleFor(x=>x.Line2, x=>x.Random.Int(1, 6) == 3 ? x.Address.SecondaryAddress() : null)
                .RuleFor(x => x.City, x => x.Address.City())
                .RuleFor(x => x.Country, x => x.Address.Country())
                .RuleFor(x => x.Zip, x => x.Address.ZipCode());

            var userGen = new Bogus.Faker<User>()
                .StrictMode(true)
                .Ignore(x=>x.Id)
                .RuleFor(x => x.Name, x => x.Name.FullName())
                .RuleFor(x => x.Email, x => x.Person.Email)
                .RuleFor(x => x.LastMessage, x => x.Hacker.Phrase())
                .RuleFor(x => x.Friends, x => x.Make(x.Random.Number(2,6), () => x.Internet.UserName()))
                .RuleFor(x => x.Address, x => addressGen.Generate());


            var users = userGen.Generate(10_000);


            using(var store = new DocumentStore
            {
                Database = "bulk",
                Urls = new [] { args[0] }
            })
            {
                store.Initialize();

                // here to force a request for RavenDB, nothing else. So the benchmark won't have to create
                // the connection to the server, we can assume that this is already there
                store.Maintenance.Send(new Raven.Client.Documents.Operations.GetStatisticsOperation());

                var docs = int.Parse(args[1]);
                var threads = new Thread[int.Parse(args[2])];

                for (int index = 0; index < threads.Length; index++)
                {
                    threads[index] = new Thread(() => DoBulkInsert(users, store, docs));
                    threads[index].Start();
                }
                var sp = Stopwatch.StartNew();
                foreach (var thread in threads)
                {
                    thread.Join();
                }
                Console.WriteLine(sp.Elapsed);

            }
        }

        private static void DoBulkInsert(List<User> users, DocumentStore store, int docs)
        {
            using (var bulk = store.BulkInsert())
            {
                for (int i = 0; i < docs; i++)
                {
                    var user = users[i % users.Count];
                    bulk.Store(user);
                    user.Id = null; // reset the user generated id
                }
            }
        }
    }

    public class User
    {
        public string Name;
        public string Id; // generated by RavenDB client
        public string Email;
        public Address Address;
        public List<string> Friends;
        public string LastMessage;
    }

    public class Address
    {
        public string Line1, Line2, City, Country, Zip;
    }
}
