﻿using System.Net.Http;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations;
using Raven.Client.Http;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.ServerWide.Operations
{
    public class DeleteOngoingTaskOperation : IMaintenanceOperation<ModifyOngoingTaskResult>
    {
        private readonly long _taskId;
        private readonly OngoingTaskType _taskType;

        public DeleteOngoingTaskOperation(long taskId, OngoingTaskType taskType)
        {
            _taskId = taskId;
            _taskType = taskType;
        }

        public RavenCommand<ModifyOngoingTaskResult> GetCommand(DocumentConventions conventions, JsonOperationContext ctx)
        {
            return new DeleteOngoingTaskCommand(_taskId, _taskType);
        }

        private class DeleteOngoingTaskCommand : RavenCommand<ModifyOngoingTaskResult>
        {
            private readonly long _taskId;
            private readonly OngoingTaskType _taskType;

            public DeleteOngoingTaskCommand(long taskId, OngoingTaskType taskType)
            {
                _taskId = taskId;
                _taskType = taskType;
            }

            public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/admin/tasks?id={_taskId}&type={_taskType}";

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete
                };

                return request;
            }

            public override void SetResponse(JsonOperationContext context, BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                Result = JsonDeserializationClient.ModifyOngoingTaskResult(response);
            }

            public override bool IsReadRequest => false;
        }
    }
}