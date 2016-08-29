﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite.Internal;
using Microsoft.AspNetCore.Rewrite.Logging;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Rewrite
{
    /// <summary>
    /// Represents a middleware that rewrites urls imported from mod_rewrite, UrlRewrite, and code.
    /// </summary>
    public class RewriteMiddleware
    {
        private static readonly Task CompletedTask = Task.FromResult(0);

        private readonly RequestDelegate _next;
        private readonly RewriteOptions _options;
        private readonly IFileProvider _fileProvider;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new instance of <see cref="RewriteMiddleware"/> 
        /// </summary>
        /// <param name="next">The delegate representing the next middleware in the request pipeline.</param>
        /// <param name="hostingEnvironment">The Hosting Environment.</param>
        /// <param name="loggerFactory">The Logger Factory.</param>
        /// <param name="options">The middleware options, containing the rules to apply.</param>
        public RewriteMiddleware(
            RequestDelegate next, 
            IHostingEnvironment hostingEnvironment, 
            ILoggerFactory loggerFactory, 
            RewriteOptions options)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _next = next;
            _options = options;
            _fileProvider = _options.StaticFileProvider ?? hostingEnvironment.WebRootFileProvider;
            _logger = loggerFactory.CreateLogger<RewriteMiddleware>();
        }

        /// <summary>
        /// Executes the middleware.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <returns>A task that represents the execution of this middleware.</returns>
        public Task Invoke(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            var rewriteContext = new RewriteContext {
                HttpContext = context,
                StaticFileProvider = _fileProvider,
                Logger = _logger,
                Result = RuleTermination.Continue
            };

            foreach (var rule in _options.Rules)
            {
                rule.ApplyRule(rewriteContext);
                switch (rewriteContext.Result)
                {
                    case RuleTermination.Continue:
                        _logger.RewriteMiddlewareRequestContinueResults();
                        break;
                    case RuleTermination.ResponseComplete:
                        _logger.RewriteMiddlewareRequestResponseComplete(
                            context.Response.Headers[HeaderNames.Location],
                            context.Response.StatusCode);
                        return CompletedTask;
                    case RuleTermination.StopRules:
                        _logger.RewriteMiddlewareRequestStopRules();
                        return _next(context);
                    default:
                        throw new ArgumentOutOfRangeException($"Invalid rule termination {rewriteContext.Result}");
                }
            }
            return _next(context);
        }
    }
}