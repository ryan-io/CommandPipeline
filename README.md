<a name="readme-top"></a>
<!-- PROJECT SHIELDS -->
<!--
*** I'm using markdown "reference style" links for readability.
*** Reference links are enclosed in brackets [ ] instead of parentheses ( ).
*** See the bottom of this document for the declaration of the reference variables
*** for contributors-url, forks-url, etc. This is an optional, concise syntax you may use.
*** https://www.markdownguide.org/basic-syntax/#reference-style-links
-->


<!-- PROJECT LOGO -->

<p align="center">
<img width="300" height="125" src="https://i.imgur.com/w5hcUtR.png">
</p>

<div align="center">

<h3 align="center">Command Pipeline</h3>

  <p align="center">
    An simple asynchronous EventHandler stream.
    <br />
    <br />
    <a href="https://github.com//ryan-io/CommandPipeline/issues">Report Bug</a>
    ·
    <a href="https://github.com//ryan-io/CommandPipeline/issues">Request Feature</a>
  </p>
</div>

---
<!-- TABLE OF CONTENTS -->

<details align="center">
  <summary>Table of Contents</summary>
  <ol>
  <li>
      <a href="#overview">Overview</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#prerequisites-and-dependencies">Prerequisites and Dependencies</a></li>
        <li><a href="#installation">Installation</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage & Examples</a></li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#contact">Contact</a></li>
    <li><a href="#acknowledgments-and-credit">Acknowledgments</a></li>
  </ol>
</details>

---

<!-- ABOUT THE PROJECT -->

# Overview

<p align="center">
<img  src="https://i.imgur.com/pLcCC4N.png" width="1200"/>
</p>

My goal for this project was to create a simple, efficient, and small API for handling events in .NET ecosystems asynchronously. This project does not utilize RX or .NET's built in Observer system. Instead, the goal was to create an EventHandler with the following characteristics:
- Returns a Task
- Is asynchronous (awaitable)
- Allows for various callbacks in the pipeline
- Catches and propagates Exceptions
- Is agnostic to the project type
- Keep it lightweight
- Does not use RX or TPL Dataflow

##### Features
<ol>
<li>
Subscribe to various processes within a pipeline: OnStart, OnStartAsync, OnSignalAsync, OnEnd, OnEndAsync, OnFinally, OnFinallyAsync, OnErrorCaught and OnErrorThrown
</li>
<li>
 API returns an awaitable Task when 'SignalAsync' is invoked
</li>
<li>
You can choose to skip running the entire pipeline process and simply invoke specific pipeline processes (such as a specific callback)
</li>
<li>
The auxiliary callbacks are completley optional, but allow the consumer to prep data, ensure an asynchronous method is in an appropriate state before firing, and exception handling.
</li>
<li>
Is application agnostic (.NET)
</li>
</ol>

<p align="right">(<a href="#readme-top">back to top</a>)</p>

# Built With
- JetBrains Rider
- Tested with WPF & Blazor WASM

<p align="right">(<a href="#readme-top">back to top</a>)</p>


<!-- GETTING STARTED -->
# Getting Started
Clone/fork this repository and add a reference to the Command Pipeline project.
OR, add this as a Nuget package to your project.

You can instantiate CommandPipelines as needed:

```csharp
var pipeline = new CommandPipeline();
```

This project supports logging via  Microsoft.Extensions.Logging.ILogger. The base constructor for CommandPipeline accepts an ILogger:

```csharp
ILogger logger   = new Logger<CommandPipeline>(new LoggerFactory());  
var     pipeline = new CommandPipeline(logger);
```

Once a pipeline instance is created, access to the fluent API becomes available:

<p align="center">
<img  src="https://i.imgur.com/FoHoE4E.png" width="300"/>
</p>
<p align="right">(<a href="#readme-top">back to top</a>)</p>


# Prerequisites and Dependencies

- .NET 6
* C# 10
* Microsoft.Extensions.Logging.7.0.0

##### Please feel free to contact me with any issues or concerns in regards to the dependencies defined above. We can work around the majority of them if needed.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

# Installation

- Clone or fork this repository. Once done, add a reference to this library in your project
- Download the latest dll and create a reference to it in your project
- Install via NPM

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- USAGE EXAMPLES -->
# Usage

##### Standalone Instance(s)

```csharp

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

```

<!-- ROADMAP -->
# Roadmap

There is currently no future features planned.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- CONTRIBUTING -->
# Contributing

Contributions are absolutely welcome. This is an open source project. 

1. Fork the repository
2. Create a feature branch
```Shell
git checkout -b feature/your-feature-branch
```
3. Commit changes on your feature branch
```Shell
git commit -m 'Summary feature'
```
4. Push your changes to your branch
```Shell
git push origin feature/your-feature-branch
```
5. Open a pull request to merge/incorporate your feature

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- LICENSE -->
# License

Distributed under the MIT License.

<p align="right">(<a href="#readme-top">back to top</a>)</p>


<!-- CONTACT -->
# Contact

<p align="center">
<b><u>RyanIO</u></b> 
<br/><br/> 
<a href = "mailto:ryan.io@programmer.net?subject=[RIO]%20Procedural%20Generator%20Project" >[Email]</a>
<br/>
[LinkedIn]
<br/>
<a href="https://github.com/ryan-io">[GitHub]</a></p>

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- ACKNOWLEDGMENTS -->
# Acknowledgments and Credit

* [Stephen Cleary's Blog](https://blog.stephencleary.com/)
	* In particular, his blog on [Async Events in OOP](https://blog.stephencleary.com/2013/02/async-oop-5-events.html) provided me with inspiration for this.

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[contributors-shield]: https://img.shields.io/github/contributors/github_username/repo_name.svg?style=for-the-badge
[contributors-url]: https://github.com/github_username/repo_name/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/github_username/repo_name.svg?style=for-the-badge
[forks-url]: https://github.com/github_username/repo_name/network/members
[stars-shield]: https://img.shields.io/github/stars/github_username/repo_name.svg?style=for-the-badge
[stars-url]: https://github.com/github_username/repo_name/stargazers
[issues-shield]: https://img.shields.io/github/issues/github_username/repo_name.svg?style=for-the-badge
[issues-url]: https://github.com/github_username/repo_name/issues
[license-shield]: https://img.shields.io/github/license/github_username/repo_name.svg?style=for-the-badge
[license-url]: https://github.com/github_username/repo_name/blob/master/LICENSE.txt
[linkedin-shield]: https://img.shields.io/badge/-LinkedIn-black.svg?style=for-the-badge&logo=linkedin&colorB=555
[linkedin-url]: https://linkedin.com/in/linkedin_username
[product-screenshot]: images/screenshot.png
[Next.js]: https://img.shields.io/badge/next.js-000000?style=for-the-badge&logo=nextdotjs&logoColor=white
[Next-url]: https://nextjs.org/
[React.js]: https://img.shields.io/badge/React-20232A?style=for-the-badge&logo=react&logoColor=61DAFB
[React-url]: https://reactjs.org/
[Vue.js]: https://img.shields.io/badge/Vue.js-35495E?style=for-the-badge&logo=vuedotjs&logoColor=4FC08D
[Vue-url]: https://vuejs.org/
[Angular.io]: https://img.shields.io/badge/Angular-DD0031?style=for-the-badge&logo=angular&logoColor=white
[Angular-url]: https://angular.io/
[Svelte.dev]: https://img.shields.io/badge/Svelte-4A4A55?style=for-the-badge&logo=svelte&logoColor=FF3E00
[Svelte-url]: https://svelte.dev/
[Laravel.com]: https://img.shields.io/badge/Laravel-FF2D20?style=for-the-badge&logo=laravel&logoColor=white
[Laravel-url]: https://laravel.com
[Bootstrap.com]: https://img.shields.io/badge/Bootstrap-563D7C?style=for-the-badge&logo=bootstrap&logoColor=white
[Bootstrap-url]: https://getbootstrap.com
[JQuery.com]: https://img.shields.io/badge/jQuery-0769AD?style=for-the-badge&logo=jquery&logoColor=white
[JQuery-url]: https://jquery.com
