﻿using System.IO;
using System.Threading.Tasks;
using FastTests;
using Raven.Client.Documents.Smuggler;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_11664 : RavenTestBase
    {
        [Fact]
        public async Task ShouldWork()
        {
            using (var store = GetDocumentStore())
            {
                using (var stream = GetDump("RavenDB_11664.1.ravendbdump"))
                {
                    await store.Smuggler.ImportAsync(new DatabaseSmugglerImportOptions(), stream);
                }

                using (var session = store.OpenAsyncSession())
                {
                    var employee = await session.LoadAsync<Employee>("employees/9-A");
                    Assert.NotNull(employee);

                    session.Delete(employee);

                    await session.SaveChangesAsync();
                }

                using (var stream = GetDump("RavenDB_11664.1.ravendbdump"))
                {
                    await store.Smuggler.ImportAsync(new DatabaseSmugglerImportOptions(), stream);
                }

                using (var session = store.OpenAsyncSession())
                {
                    var employee = await session.LoadAsync<Employee>("employees/9-A");
                    Assert.NotNull(employee);

                    session.Delete(employee);

                    await session.SaveChangesAsync();
                }
            }
        }

        private static Stream GetDump(string name)
        {
            var assembly = typeof(RavenDB_9912).Assembly;
            return assembly.GetManifestResourceStream("SlowTests.Data." + name);
        }
    }
}
