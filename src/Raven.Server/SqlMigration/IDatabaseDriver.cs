﻿using System;
using System.Threading.Tasks;
using Raven.Server.Documents;
using Raven.Server.ServerWide.Context;
using Raven.Server.SqlMigration.Model;
using Raven.Server.SqlMigration.Schema;

namespace Raven.Server.SqlMigration
{
    public interface IDatabaseDriver
    {
        DatabaseSchema FindSchema();
        
        Task Migrate(MigrationSettings settings, DatabaseSchema schema, DocumentDatabase db, DocumentsOperationContext context);
    }
}