# Redaction

This example shows how to redact sensitive information from Logs.
In this example, we attach a custom `Processor` called `MyRedactionProcessor`
which is responsible for replacing any instance of the word "&lt;secret&gt;"
with the value "newRedactedValueHere".
