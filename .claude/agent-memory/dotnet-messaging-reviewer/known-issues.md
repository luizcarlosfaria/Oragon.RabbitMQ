# Known correctness issues (as of 2026-06-15 review)

## DynamicQueueConsumer.cs
- After `QueueDeclarePassiveAsync` throws `OperationInterruptedException` (queue missing), RabbitMQ CLOSES the channel. The `finally` calls CloseChannelAsync which guards on IsOpen — OK. But within the happy path, `TryGetRemainingReadyCountAsync` does a second QueueDeclarePassive on the SAME channel near end; if anything closed it the catch swallows -> returns null (acceptable, best-effort).
- `noLocal: true` on BasicConsumeAsync (line ~325) AND in QueueConsumer.StartAsync (line 214). RabbitMQ does NOT implement no_local; silently ignored by broker. Misleading, not a hard failure.
- Race: `consumer.ReceivedAsync` acquires `localConcurrency.WaitAsync` BEFORE checking `completion.Task.IsCompleted`. If multiple deliveries in flight when stopping, messages get Nack(requeue:true) -> back to head. Also `completion.Task.IsCompleted` check is racy vs TryComplete.
- ShutdownAsync handler sets status Interrupted even on a clean BasicCancel-driven shutdown (brokerCanceled=true), can mislabel normal stop as Interrupted.

## QueueArguments
- Subclasses `Dictionary<string,object>` and is handed directly to QueueDeclare as IDictionary — caller can mutate broker-bound args. Mutable shared topology dictionary.

## QueueArgumentDiagnostics.ValuesEqual
- Numeric coercion via Convert.ToDecimal treats e.g. int 10 == long 10 == byte 10 as equal. Fine for x-max-priority but x-queue-type is string so unaffected. bool not in IsNumber list -> bool compared via .Equals (OK).
