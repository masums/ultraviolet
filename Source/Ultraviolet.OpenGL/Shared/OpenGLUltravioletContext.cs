﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Ultraviolet.Content;
using Ultraviolet.Core;
using Ultraviolet.Graphics;
using Ultraviolet.SDL2;
using Ultraviolet.SDL2.Native;
using Ultraviolet.SDL2.Platform;
using Ultraviolet.UI;

namespace Ultraviolet.OpenGL
{
    /// <summary>
    /// Represents the OpenGL implementation of the Ultraviolet context.
    /// </summary>
    [CLSCompliant(true)]
    public sealed class OpenGLUltravioletContext : SDL2UltravioletContext
    {
        /// <summary>
        /// Initializes a new instance of the OpenGLUltravioletContext class.
        /// </summary>
        /// <param name="host">The object that is hosting the Ultraviolet context.</param>
        public OpenGLUltravioletContext(IUltravioletHost host)
            : this(host, OpenGLUltravioletConfiguration.Default)
        {

        }

        /// <summary>
        /// Initializes a new instance of the OpenGLUltravioletContext class.
        /// </summary>
        /// <param name="host">The object that is hosting the Ultraviolet context.</param>
        /// <param name="configuration">The Ultraviolet Framework configuration settings for this context.</param>
        public OpenGLUltravioletContext(IUltravioletHost host, OpenGLUltravioletConfiguration configuration)
            : base(host, configuration)
        {
            Contract.Require(configuration, nameof(configuration));

            IsHardwareInputDisabled = configuration.IsHardwareInputDisabled;

            if (!InitSDL(configuration))
                throw new SDL2Exception();

            var isGLEs = false;
            var versionRequested = default(Version);
            var versionRequired = default(Version);
            InitOpenGLVersion(configuration, out versionRequested, out versionRequired, out isGLEs);

            if (!configuration.EnableServiceMode)
                InitOpenGLAttributes(configuration, versionRequested, versionRequired, isGLEs);

            var sdlAssembly = typeof(SDL).Assembly;
            InitializeFactoryMethodsInAssembly(sdlAssembly);

            var sdlconfig = new SDL2PlatformConfiguration();
            sdlconfig.RenderingAPI = SDL2PlatformRenderingAPI.OpenGL;
            sdlconfig.MultiSampleBuffers = configuration.MultiSampleBuffers;
            sdlconfig.MultiSampleSamples = configuration.MultiSampleSamples;
            this.platform = IsRunningInServiceMode ? (IUltravioletPlatform)new DummyUltravioletPlatform(this) : new SDL2UltravioletPlatform(this, configuration, sdlconfig);

            PumpEvents();

            if (IsRunningInServiceMode)
            {
                this.graphics = new DummyUltravioletGraphics(this);
                this.audio = new DummyUltravioletAudio(this);
                this.input = new DummyUltravioletInput(this);
            }
            else
            {
                this.graphics = new OpenGLUltravioletGraphics(this, configuration, versionRequested);
                ((OpenGLUltravioletGraphics)this.graphics).ResetDeviceStates();
                this.audio = InitializeAudioSubsystem(configuration);
                this.input = new SDL2UltravioletInput(this);
            }

            this.content = new UltravioletContent(this);
            this.content.RegisterImportersAndProcessors(new[] { sdlAssembly, AudioSubsystemAssembly });
            this.content.Importers.RegisterImporter<XmlContentImporter>("prog");

            this.ui = new UltravioletUI(this, configuration);

            PumpEvents();
            
            InitializeContext();
            InitializeViewProvider(configuration);
        }

        /// <inheritdoc/>
        public override void Draw(UltravioletTime time)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            OnDrawing(time);

            var oglgfx = graphics as OpenGLUltravioletGraphics;
            if (oglgfx != null)
            {
                var glcontext = oglgfx.OpenGLContext;
                var windowInfo = (SDL2UltravioletWindowInfoOpenGL)platform.Windows;
                foreach (var window in platform.Windows)
                {
                    windowInfo.DesignateCurrent(window, glcontext);

                    window.Compositor.BeginFrame();
                    window.Compositor.BeginContext(CompositionContext.Scene);

                    OnWindowDrawing(time, window);

                    windowInfo.Draw(time);

                    OnWindowDrawn(time, window);

                    window.Compositor.Compose();
                    window.Compositor.Present();

                    windowInfo.Swap();
                }

                windowInfo.DesignateCurrent(null, glcontext);

                oglgfx.SetRenderTargetToBackBuffer();
                oglgfx.UpdateFrameRate();
            }

            base.Draw(time);
        }

        /// <inheritdoc/>
        public override IUltravioletPlatform GetPlatform()
        {
            Contract.EnsureNotDisposed(this, Disposed);

            return platform;
        }

        /// <inheritdoc/>
        public override IUltravioletContent GetContent()
        {
            Contract.EnsureNotDisposed(this, Disposed);

            return content;
        }

        /// <inheritdoc/>
        public override IUltravioletGraphics GetGraphics()
        {
            Contract.EnsureNotDisposed(this, Disposed);

            return graphics;
        }

        /// <inheritdoc/>
        public override IUltravioletAudio GetAudio()
        {
            Contract.EnsureNotDisposed(this, Disposed);

            return audio;
        }

        /// <inheritdoc/>
        public override IUltravioletInput GetInput()
        {
            Contract.EnsureNotDisposed(this, Disposed);

            return input;
        }

        /// <inheritdoc/>
        public override IUltravioletUI GetUI()
        {
            Contract.EnsureNotDisposed(this, Disposed);

            return ui;
        }

        /// <summary>
        /// Gets the assembly that implements the audio subsystem.
        /// </summary>
        public Assembly AudioSubsystemAssembly
        {
            get
            {
                Contract.EnsureNotDisposed(this, Disposed);

                return audioSubsystemAssembly;
            }
        }

        /// <summary>
        /// Initializes the context's audio subsystem.
        /// </summary>
        /// <param name="configuration">The Ultraviolet Framework configuration settings for this context.</param>
        /// <returns>The audio subsystem.</returns>
        private IUltravioletAudio InitializeAudioSubsystem(OpenGLUltravioletConfiguration configuration)
        {
            if (String.IsNullOrEmpty(configuration.AudioSubsystemAssembly))
                throw new InvalidOperationException(OpenGLStrings.InvalidAudioAssembly);

            Assembly asm;
            try
            {
                asm = Assembly.Load(configuration.AudioSubsystemAssembly);
                InitializeFactoryMethodsInAssembly(asm);
                audioSubsystemAssembly = asm;
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException ||
                    e is FileLoadException ||
                    e is BadImageFormatException)
                {
                    throw new InvalidOperationException(OpenGLStrings.InvalidAudioAssembly, e);
                }
                throw;
            }

            var types = (from t in asm.GetTypes()
                         where
                          t.IsClass && !t.IsAbstract &&
                          t.GetInterfaces().Contains(typeof(IUltravioletAudio))
                         select t).ToList();

            if (!types.Any() || types.Count > 1)
                throw new InvalidOperationException(OpenGLStrings.InvalidAudioAssembly);

            var type = types.Single();

            var ctorWithConfig = type.GetConstructor(new[] { typeof(UltravioletContext), typeof(UltravioletConfiguration) });
            if (ctorWithConfig != null)
            {
                return (IUltravioletAudio)ctorWithConfig.Invoke(new object[] { this, configuration });
            }

            var ctorWithoutConfig = type.GetConstructor(new[] { typeof(UltravioletContext) });
            if (ctorWithoutConfig != null)
            {
                return (IUltravioletAudio)ctorWithoutConfig.Invoke(new object[] { this });
            }

            throw new InvalidOperationException(OpenGLStrings.InvalidAudioAssembly);
        }

        /// <summary>
        /// Determines which version of OpenGL will be used by the context.
        /// </summary>
        private void InitOpenGLVersion(OpenGLUltravioletConfiguration configuration,
            out Version versionRequested, out Version versionRequired, out Boolean isGLES)
        {
            isGLES = (Platform == UltravioletPlatform.Android || Platform == UltravioletPlatform.iOS);

            versionRequired = isGLES ? new Version(2, 0) : new Version(3, 1);
            versionRequested = isGLES ? configuration.MinimumOpenGLESVersion : configuration.MinimumOpenGLVersion;
            if (versionRequested == null || versionRequested < versionRequired)
            {
                if (isGLES)
                {
                    versionRequested = Platform == UltravioletPlatform.Android ?
                        new Version(2, 0) : new Version(3, 0);
                }
                else
                {
                    versionRequested = versionRequired;
                }
            }
        }

        /// <summary>
        /// Sets the SDL2 attributes which correspond to the application's OpenGL settings.
        /// </summary>
        private void InitOpenGLAttributes(OpenGLUltravioletConfiguration configuration, 
            Version versionRequested, Version versionRequired, Boolean isGLES)
        {
            var profile = isGLES ? SDL_GLprofile.ES : SDL_GLprofile.CORE;

            if (SDL.GL_SetAttribute(SDL_GLattr.CONTEXT_PROFILE_MASK, (Int32)profile) < 0)
                throw new SDL2Exception();

            // NOTE: Asking for an ES 3.0 context in the emulator will return a valid
            // context pointer, but actually using it will cause segfaults. It seems like
            // the best thing to do on Android is just not ask for a specific version,
            // and trust the OS to give you the highest version it supports.
            if (Platform != UltravioletPlatform.Android)
            {
                if (SDL.GL_SetAttribute(SDL_GLattr.CONTEXT_MAJOR_VERSION, versionRequested.Major) < 0)
                    throw new SDL2Exception();

                if (SDL.GL_SetAttribute(SDL_GLattr.CONTEXT_MINOR_VERSION, versionRequested.Minor) < 0)
                    throw new SDL2Exception();
            }

            if (SDL.GL_SetAttribute(SDL_GLattr.DEPTH_SIZE, configuration.BackBufferDepthSize) < 0)
                throw new SDL2Exception();

            if (SDL.GL_SetAttribute(SDL_GLattr.STENCIL_SIZE, configuration.BackBufferStencilSize) < 0)
                throw new SDL2Exception();

            if (SDL.GL_SetAttribute(SDL_GLattr.RETAINED_BACKING, 0) < 0)
                throw new SDL2Exception();

            if (configuration.Use32BitFramebuffer)
            {
                if (SDL.GL_SetAttribute(SDL_GLattr.RED_SIZE, 8) < 0)
                    throw new SDL2Exception();

                if (SDL.GL_SetAttribute(SDL_GLattr.GREEN_SIZE, 8) < 0)
                    throw new SDL2Exception();

                if (SDL.GL_SetAttribute(SDL_GLattr.BLUE_SIZE, 8) < 0)
                    throw new SDL2Exception();
            }
            else
            {
                if (SDL.GL_SetAttribute(SDL_GLattr.RED_SIZE, 5) < 0)
                    throw new SDL2Exception();

                if (SDL.GL_SetAttribute(SDL_GLattr.GREEN_SIZE, 6) < 0)
                    throw new SDL2Exception();

                if (SDL.GL_SetAttribute(SDL_GLattr.BLUE_SIZE, 5) < 0)
                    throw new SDL2Exception();
            }
        }

        // Ultraviolet subsystems.
        private readonly IUltravioletPlatform platform;
        private readonly IUltravioletContent content;
        private readonly IUltravioletGraphics graphics;
        private readonly IUltravioletAudio audio;
        private readonly IUltravioletInput input;
        private readonly IUltravioletUI ui;

        // Property values.
        private Assembly audioSubsystemAssembly;
    }
}
