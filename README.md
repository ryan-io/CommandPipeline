using Rio.CommandPipeline;

public class Test {
	public async Task MyTestMethod() {
		// Create a new command pipeline
		var pipeline = new CommandPipeline();

		// Register a command with the pipeline
		// The method signature must be CommandPipeline.CommandPipelineDelegate:
		// delegate Task PipelineDelegate(object? o, PipelineObject? pObj, CancellationToken token)
		pipeline.RegisterWork(MyWorkMethodAsync);
		
		// Register optional callbacks
		// Read through the available fluent methods to see what you can do
		pipeline.RegisterOnStart(OnStart)
		        .RegisterOnEnd(OnEnd);
		
		// The fluent API allows you to chain method calls
		
		// We can also register asynchronous callbacks
		// Register asynchronous callbacks requires the method signature to match CommandPipeline.CommandPipelineDelegate
		pipeline.RegisterOnStartAsync(OnStartAsync)
		        .RegisterOnEndAsync(OnEndAsync);
		
		// Start the pipeline
		// PipelineObject are analogous to EventArgs
		// You should create a derived class from PipelineObject to allow for transmission through the pipeline
		// This class should contain any required information or state for the work registered to the pipeline; 
		// you decide.
		await pipeline.SignalAsync(new PipelineObject());
		// await pipeline.SignalAsync(PipelineObject.Empty);
		
		// You can rerun this pipeline as needed.
		// If needed, unregister any work or callbacks
		pipeline.UnregisterOnStartAsync(OnStartAsync);
		pipeline.UnregisterOnEnd(OnEnd);
		
		// All fluent API methods allow for multiple registrations
		pipeline.RegisterWork(MyWorkMethodAsync, MyWorkMethodAsync, MyWorkMethodAsync);
		pipeline.RegisterOnStart(OnStart, OnStart, OnStart, OnStart);
		
		// Errors will be caught and thrown
		// ErrorCaught is invoked first, followed by ErrorThrown
		// This allows you to handle errors as they are caught, followed by thrown
		pipeline.RegisterOnErrorCaught(OnErrorCaught);
		pipeline.RegisterOnErrorThrown(OnErrorThrown);
	}

	async Task MyWorkMethodAsync(object? sender, PipelineObject? pipelineObject, CancellationToken token) {
		// Do some work
		await Task.Delay(5000, token);
	}
	
	void OnStart(PipelineObject? pipelineObject) {
		// Do something when the pipeline starts
	}
	
	void OnEnd(PipelineObject? pipelineObject) {
		// Do something when the pipeline ends
	}
	
	void OnErrorCaught(PipelineObject? pipelineObject, Exception exception) {
		// Do something when an exception is caught
	}
	
	void OnErrorThrown(PipelineObject? pipelineObject, Exception exception) {
		// Do something when an exception is thrown
	}

	Task OnStartAsync(object?  sender, PipelineObject? pipelineObject, CancellationToken token) {
		// Do something when the pipeline starts asynchronously
		return Task.CompletedTask;
	}

	Task OnEndAsync(object? sender, PipelineObject? pipelineObject, CancellationToken token) {
		// Do something when the pipeline ends asynchronously
		return Task.CompletedTask;
	}
}
