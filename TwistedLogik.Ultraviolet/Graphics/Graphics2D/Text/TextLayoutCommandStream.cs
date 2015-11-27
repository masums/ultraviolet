﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using TwistedLogik.Nucleus;
using TwistedLogik.Nucleus.Collections.Specialized;
using TwistedLogik.Nucleus.Text;

namespace TwistedLogik.Ultraviolet.Graphics.Graphics2D.Text
{
    /// <summary>
    /// Represents a stream of commands produced by the text layout engine.
    /// </summary>
    [SecuritySafeCritical]
    public unsafe class TextLayoutCommandStream
    {
        /// <summary>
        /// Moves the stream to the command with the specified index.
        /// </summary>
        /// <param name="index">The index of the command to which the stream will seek.</param>
        /// <returns>A <see cref="TextLayoutCommandType"/> that represents the type of command at the stream's new position.</returns>
        public TextLayoutCommandType Seek(Int32 index)
        {
            var data = stream.SeekObject(index);
            streamPosition = index;
            return *(TextLayoutCommandType*)data;
        }
        
        /// <summary>
        /// Moves the stream to the first command in the specified line of text.
        /// </summary>
        /// <param name="index">The index of the line to which to seek.</param>
        /// <returns>A <see cref="TextLayoutCommandType"/> that represents the type of command at the stream's new position.</returns>
        public TextLayoutCommandType SeekLine(Int32 index)
        {
            Contract.EnsureRange(index >= 0 && index < LineCount, "index");
            
            var position = 1;

            for (int i = 0; i <= index; i++)
            {
                stream.SeekObject(position);
                position += ((TextLayoutLineInfoCommand*)stream.Data)->LengthInCommands;
            }

            return *(TextLayoutCommandType*)stream.Data;
        }

        /// <summary>
        /// Moves the stream to the next command.
        /// </summary>
        /// <returns><c>true</c> if the stream was able to seek to the next command; otherwise, <c>false</c>.</returns>
        public Boolean SeekNextCommand()
        {
            if (streamPosition == Count)
                return false;

            switch (*(TextLayoutCommandType*)stream.Data)
            {
                case TextLayoutCommandType.BlockInfo:
                    stream.Seek(sizeof(TextLayoutBlockInfoCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.LineInfo:
                    stream.Seek(sizeof(TextLayoutLineInfoCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.Text:
                    stream.Seek(sizeof(TextLayoutTextCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.Icon:
                    stream.Seek(sizeof(TextLayoutIconCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.ToggleBold:
                    stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.ToggleItalic:
                    stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.PushStyle:
                    stream.Seek(sizeof(TextLayoutStyleCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.PushFont:
                    stream.Seek(sizeof(TextLayoutFontCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.PushColor:
                    stream.Seek(sizeof(TextLayoutColorCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.PushGlyphShader:
                    stream.Seek(sizeof(TextLayoutGlyphShaderCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.PopStyle:
                    stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.PopFont:
                    stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.PopColor:
                    stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.PopGlyphShader:
                    stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.ChangeSourceString:
                    stream.Seek(sizeof(TextLayoutSourceStringCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.ChangeSourceStringBuilder:
                    stream.Seek(sizeof(TextLayoutSourceStringBuilderCommand), SeekOrigin.Current);
                    break;

                case TextLayoutCommandType.Hyphen:
                    stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
                    break;

                default:
                    if (streamPosition + 1 <= Count)
                    {
                        Seek(streamPosition + 1);
                        return true;
                    }
                    return false;
            }

            streamPosition++;
            return true;
        }

        /// <summary>
        /// Moves the stream to the first command in the next line of text, if there is one.
        /// </summary>
        /// <returns><c>true</c> if the stream was able to seek to another line of text; otherwise, <c>false</c>.</returns>
        public Boolean SeekNextLine()
        {
            var currentCommandType = *(TextLayoutCommandType*)stream.Data;
            if (currentCommandType == TextLayoutCommandType.LineInfo)
            {
                Seek(streamPosition + ((TextLayoutLineInfoCommand*)stream.Data)->LengthInCommands + 1);
            }
            else
            {
                while (*(TextLayoutCommandType*)stream.Data != TextLayoutCommandType.LineInfo && SeekNextCommand()) { }
            }
            return streamPosition + 1 < Count;
        }

        /// <summary>
        /// Registers a style with the command stream.
        /// </summary>
        /// <param name="name">The name of the style to register.</param>
        /// <param name="style">The style to register under the specified name.</param>
        /// <returns>The index of the specified style within the command stream's internal registry.</returns>
        public Int32 RegisterStyle(StringSegment name, TextStyle style)
        {
            Contract.Require(style, "style");

            return RegisterResource(name, style, styles, stylesByName);
        }

        /// <summary>
        /// Registers an icon with the command stream.
        /// </summary>
        /// <param name="name">The name of the icon to register.</param>
        /// <param name="icon">The icon to register under the specified name.</param>
        /// <returns>The index of the specified icon within the command stream's internal registry.</returns>
        public Int32 RegisterIcon(StringSegment name, TextIconInfo icon)
        {
            return RegisterResource(name, icon, icons, iconsByName);
        }

        /// <summary>
        /// Registers a font with the command stream.
        /// </summary>
        /// <param name="name">The name of the font to register.</param>
        /// <param name="font">The font to register under the specified name.</param>
        /// <returns>The index of the specified font within the command stream's internal registry.</returns>
        public Int32 RegisterFont(StringSegment name, SpriteFont font)
        {
            Contract.Require(font, "font");

            return RegisterResource(name, font, fonts, fontsByName);
        }

        /// <summary>
        /// Registers a glyph shader with the command stream.
        /// </summary>
        /// <param name="name">The name of the glyph shader to register.</param>
        /// <param name="glyphShader">The glyph shader to register under the specified name.</param>
        /// <returns>The index of the specified glyph shader within the command stream's internal registry.</returns>
        public Int32 RegisterGlyphShader(StringSegment name, GlyphShader glyphShader)
        {
            Contract.Require(glyphShader, "glyphShader");

            return RegisterResource(name, glyphShader, glyphShaders, glyphShadersByName);
        }

        /// <summary>
        /// Registers a source string with the command stream.
        /// </summary>
        /// <param name="source">The source string to register.</param>
        /// <returns>The index of the specified source string within the command stream's internal registry.</returns>
        public Int32 RegisterSourceString(String source)
        {
            Contract.Require(source, "source");

            return RegisterSource(source);
        }

        /// <summary>
        /// Registers a source string builder with the command stream.
        /// </summary>
        /// <param name="source">The source string builder to register.</param>
        /// <returns>The index of the specified source string builder within the command stream's internal registry.</returns>
        public Int32 RegisterSourceStringBuilder(StringBuilder source)
        {
            Contract.Require(source, "source");

            return RegisterSource(source);
        }

        /// <summary>
        /// Retrieves the registered style with the specified name.
        /// </summary>
        /// <param name="name">The name of the style to retrieve.</param>
        /// <returns>The registered style with the specified name.</returns>
        public TextStyle GetStyle(StringSegment name)
        {
            Int32 index;
            if (!stylesByName.TryGetValue(name, out index))
                return null;

            return styles[index];
        }

        /// <summary>
        /// Retrieves the registered style at the specified index within the command stream's internal registry.
        /// </summary>
        /// <param name="index">The index of the registered style to retrieve.</param>
        /// <returns>The registered style at the specified index within the command stream's internal registry.</returns>
        public TextStyle GetStyle(Int32 index)
        {
            return styles[index];
        }

        /// <summary>
        /// Retrieves the registered icon with the specified name.
        /// </summary>
        /// <param name="name">The name of the icon to retrieve.</param>
        /// <returns>The registered icon with the specified name.</returns>
        public TextIconInfo? GetIcon(StringSegment name)
        {
            Int32 index;
            if (!iconsByName.TryGetValue(name, out index))
                return null;

            return icons[index];
        }

        /// <summary>
        /// Retrieves the registered icon at the specified index within the command stream's internal registry.
        /// </summary>
        /// <param name="index">The index of the registered icon to retrieve.</param>
        /// <returns>The registered icon at the specified index within the command stream's internal registry.</returns>
        public TextIconInfo GetIcon(Int32 index)
        {
            return icons[index];
        }

        /// <summary>
        /// Retrieves the registered font with the specified name.
        /// </summary>
        /// <param name="name">The name of the font to retrieve.</param>
        /// <returns>The registered font with the specified name.</returns>
        public SpriteFont GetFont(StringSegment name)
        {
            Int32 index;
            if (!fontsByName.TryGetValue(name, out index))
                return null;

            return fonts[index];
        }

        /// <summary>
        /// Retrieves the registered font at the specified index within the command stream's internal registry.
        /// </summary>
        /// <param name="index">The index of the registered font to retrieve.</param>
        /// <returns>The registered font at the specified index within the command stream's internal registry.</returns>
        public SpriteFont GetFont(Int32 index)
        {
            return fonts[index];
        }

        /// <summary>
        /// Retrieves the registered glyph shader with the specified name.
        /// </summary>
        /// <param name="name">The name of the glyph shader to retrieve.</param>
        /// <returns>The registered glyph shader with the specified name.</returns>
        public GlyphShader GetGlyphShader(StringSegment name)
        {
            Int32 index;
            if (!glyphShadersByName.TryGetValue(name, out index))
                return null;

            return glyphShaders[index];
        }

        /// <summary>
        /// Retrieves the registered glyph shader at the specified index within the command stream's internal registry.
        /// </summary>
        /// <param name="index">The index of the registered glyph shader to retrieve.</param>
        /// <returns>The registered glyph shader at the specified index within the command stream's internal registry.</returns>
        public GlyphShader GetGlyphShader(Int32 index)
        {
            return glyphShaders[index];
        }

        /// <summary>
        /// Retrieves the registered source string at the specified index within the command stream's internal registry.
        /// </summary>
        /// <param name="index">The index of the registered source string to retrieve.</param>
        /// <returns>The registered source string at the specified index within the command stream's internal registry.</returns>
        public String GetSourceString(Int32 index)
        {
            return (String)sources[index];
        }

        /// <summary>
        /// Retrieves the registered source string builder at the specified index within the command stream's internal registry.
        /// </summary>
        /// <param name="index">The index of the registered source string builder to retrieve.</param>
        /// <returns>The registered source string builder at the specified index within the command stream's internal registry.</returns>
        public StringBuilder GetSourceStringBuilder(Int32 index)
        {
            return (StringBuilder)sources[index];
        }

        /// <summary>
        /// Prepares the stream for reading or writing by acquiring pointers to its underlying buffers.
        /// While pointers are acquired, these buffers will be pinned in memory.
        /// </summary>
        public void AcquirePointers()
        {
            stream.AcquirePointers();
        }

        /// <summary>
        /// Releases the pointers which were acquired by <see cref="AcquirePointers()"/> and unpins
        /// the stream's underlying buffers.
        /// </summary>
        public void ReleasePointers()
        {
            stream.ReleasePointers();
        }

        /// <summary>
        /// Removes all commands from the stream.
        /// </summary>
        public void Clear()
        {
            streamPosition = 0;

            stream.Clear();

            styles.Clear();
            stylesByName.Clear();

            icons.Clear();
            iconsByName.Clear();

            fonts.Clear();
            fontsByName.Clear();

            glyphShaders.Clear();
            glyphShadersByName.Clear();

            sources.Clear();
            sourcesByReference.Clear();

            Settings = default(TextLayoutSettings);
            SourceText = StringSegment.Empty;
            Bounds = default(Rectangle);
            ActualWidth = 0;
            ActualHeight = 0;
            TotalLength = 0;
            LineCount = 0;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.BlockInfo"/> command to the current position in the stream.
        /// </summary>
        public void WriteBlockInfo()
        {
            stream.Reserve(sizeof(TextLayoutBlockInfoCommand));
            *(TextLayoutBlockInfoCommand*)stream.Data = new TextLayoutBlockInfoCommand();
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.BlockInfo;
            stream.FinalizeObject(sizeof(TextLayoutBlockInfoCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.LineInfo"/> command to the current position in the stream.
        /// </summary>
        public void WriteLineInfo()
        {
            stream.Reserve(sizeof(TextLayoutLineInfoCommand));
            *(TextLayoutLineInfoCommand*)stream.Data = new TextLayoutLineInfoCommand();
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.LineInfo;
            stream.FinalizeObject(sizeof(TextLayoutLineInfoCommand));
            streamPosition++;       
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.Text"/> command to the current position in the stream.
        /// </summary>
        /// <param name="command">The command to write to the stream.</param>
        public void WriteText(TextLayoutTextCommand command)
        {
            stream.Reserve(sizeof(TextLayoutTextCommand));
            *(TextLayoutTextCommand*)stream.Data = command;
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.Text;
            stream.FinalizeObject(sizeof(TextLayoutTextCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.Icon"/> command to the current position in the stream.
        /// </summary>
        /// <param name="command">The command to write to the stream.</param>
        public void WriteIcon(TextLayoutIconCommand command)
        {
            stream.Reserve(sizeof(TextLayoutIconCommand));
            *(TextLayoutIconCommand*)stream.Data = command;
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.Icon;
            stream.FinalizeObject(sizeof(TextLayoutIconCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.ToggleBold"/> command to the current position in the stream.
        /// </summary>
        public void WriteToggleBold()
        {
            stream.Reserve(sizeof(TextLayoutCommandType));
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.ToggleBold;
            stream.FinalizeObject(sizeof(TextLayoutCommandType));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.ToggleItalic"/> command to the current position in the stream.
        /// </summary>
        public void WriteToggleItalic()
        {
            stream.Reserve(sizeof(TextLayoutCommandType));
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.ToggleItalic;
            stream.FinalizeObject(sizeof(TextLayoutCommandType));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.PushStyle"/> command to the current position in the stream.
        /// </summary>
        /// <param name="command">The command to write to the stream.</param>
        public void WritePushStyle(TextLayoutStyleCommand command)
        {
            stream.Reserve(sizeof(TextLayoutStyleCommand));
            *(TextLayoutStyleCommand*)stream.Data = command;
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.PushStyle;
            stream.FinalizeObject(sizeof(TextLayoutStyleCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.PushFont"/> command to the current position in the stream.
        /// </summary>
        /// <param name="command">The command to write to the stream.</param>
        public void WritePushFont(TextLayoutFontCommand command)
        {
            stream.Reserve(sizeof(TextLayoutFontCommand));
            *(TextLayoutFontCommand*)stream.Data = command;
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.PushFont;
            stream.FinalizeObject(sizeof(TextLayoutFontCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.PushColor"/> command to the current position in the stream.
        /// </summary>
        /// <param name="command">The command to write to the stream.</param>
        public void WritePushColor(TextLayoutColorCommand command)
        {
            stream.Reserve(sizeof(TextLayoutColorCommand));
            *(TextLayoutColorCommand*)stream.Data = command;
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.PushColor;
            stream.FinalizeObject(sizeof(TextLayoutColorCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.PushGlyphShader"/> command to the current position in the stream.
        /// </summary>
        /// <param name="command">The command to write to the stream.</param>
        public void WritePushGlyphShader(TextLayoutGlyphShaderCommand command)
        {
            stream.Reserve(sizeof(TextLayoutGlyphShaderCommand));
            *(TextLayoutGlyphShaderCommand*)stream.Data = command;
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.PushGlyphShader;
            stream.FinalizeObject(sizeof(TextLayoutGlyphShaderCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.PopStyle"/> command to the current position in the stream.
        /// </summary>
        public void WritePopStyle()
        {
            stream.Reserve(sizeof(TextLayoutCommandType));
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.PopStyle;
            stream.FinalizeObject(sizeof(TextLayoutCommandType));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.PopFont"/> command to the current position in the stream.
        /// </summary>
        public void WritePopFont()
        {
            stream.Reserve(sizeof(TextLayoutCommandType));
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.PopFont;
            stream.FinalizeObject(sizeof(TextLayoutCommandType));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.PopColor"/> command to the current position in the stream.
        /// </summary>
        public void WritePopColor()
        {
            stream.Reserve(sizeof(TextLayoutCommandType));
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.PopColor;
            stream.FinalizeObject(sizeof(TextLayoutCommandType));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.PopGlyphShader"/> command to the current position in the stream.
        /// </summary>
        public void WritePopGlyphShader()
        {
            stream.Reserve(sizeof(TextLayoutCommandType));
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.PopGlyphShader;
            stream.FinalizeObject(sizeof(TextLayoutCommandType));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.ChangeSourceString"/> command to the current position in the stream.
        /// </summary>
        /// <param name="command">The command to write to the stream.</param>
        public void WriteChangeSourceString(TextLayoutSourceStringCommand command)
        {
            stream.Reserve(sizeof(TextLayoutSourceStringCommand));
            *(TextLayoutSourceStringCommand*)stream.Data = command;
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.ChangeSourceString;
            stream.FinalizeObject(sizeof(TextLayoutSourceStringCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.ChangeSourceStringBuilder"/> command to the current position in the stream.
        /// </summary>
        /// <param name="command">The command to write to the stream.</param>
        public void WriteChangeSourceStringBuilder(TextLayoutSourceStringBuilderCommand command)
        {
            stream.Reserve(sizeof(TextLayoutSourceStringBuilderCommand));
            *(TextLayoutSourceStringBuilderCommand*)stream.Data = command;
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.ChangeSourceStringBuilder;
            stream.FinalizeObject(sizeof(TextLayoutSourceStringBuilderCommand));
            streamPosition++;
        }

        /// <summary>
        /// Writes a <see cref="TextLayoutCommandType.Hyphen"/> command to the current position in the stream.
        /// </summary>
        public void WriteHyphen()
        {
            stream.Reserve(sizeof(TextLayoutCommandType));
            *(TextLayoutCommandType*)stream.Data = TextLayoutCommandType.Hyphen;
            stream.FinalizeObject(sizeof(TextLayoutCommandType));
            streamPosition++;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.BlockInfo"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutBlockInfoCommand ReadBlockInfoCommand()
        {
            var command = *(TextLayoutBlockInfoCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutBlockInfoCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.LineInfo"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutLineInfoCommand ReadLineInfoCommand()
        {
            var command = *(TextLayoutLineInfoCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutLineInfoCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.Text"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutTextCommand ReadTextCommand()
        {
            var command = *(TextLayoutTextCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutTextCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.Icon"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutIconCommand ReadIconCommand()
        {
            var command = *(TextLayoutIconCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutIconCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.ToggleBold"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public void ReadToggleBoldCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.ToggleItalic"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public void ReadToggleItalicCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.PushStyle"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutStyleCommand ReadPushStyleCommand()
        {
            var command = *(TextLayoutStyleCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutStyleCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.PushFont"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutFontCommand ReadPushFontCommand()
        {
            var command = *(TextLayoutFontCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutFontCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.PushColor"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutColorCommand ReadPushColorCommand()
        {
            var command = *(TextLayoutColorCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutColorCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.PushGlyphShader"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutGlyphShaderCommand ReadPushGlyphShaderCommand()
        {
            var command = *(TextLayoutGlyphShaderCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutGlyphShaderCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.PopStyle"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public void ReadPopStyleCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.PopFont"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public void ReadPopFontCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.PopColor"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public void ReadPopColorCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.PopGlyphShader"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public void ReadPopGlyphShaderCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.ChangeSourceString"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutSourceStringCommand ReadChangeSourceStringCommand()
        {
            var command = *(TextLayoutSourceStringCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutSourceStringCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.ChangeSourceStringBuilder"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public TextLayoutSourceStringBuilderCommand ReadChangeSourceStringBuilderCommand()
        {
            var command = *(TextLayoutSourceStringBuilderCommand*)stream.Data;
            stream.Seek(sizeof(TextLayoutSourceStringBuilderCommand), SeekOrigin.Current);
            streamPosition++;
            return command;
        }

        /// <summary>
        /// Reads a <see cref="TextLayoutCommandType.Hyphen"/> command from the current position in the command stream.
        /// </summary>
        /// <returns>The command that was read.</returns>
        public void ReadHyphenCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.BlockInfo"/> command.
        /// </summary>
        public void SeekPastBlockInfoCommand()
        {
            stream.Seek(sizeof(TextLayoutBlockInfoCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.LineInfo"/> command.
        /// </summary>
        public void SeekPastLineInfoCommand()
        {
            stream.Seek(sizeof(TextLayoutLineInfoCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.Text"/> command.
        /// </summary>
        public void SeekPastTextCommand()
        {
            stream.Seek(sizeof(TextLayoutTextCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be an <see cref="TextLayoutCommandType.Icon"/> command.
        /// </summary>
        public void SeekPastIconCommand()
        {
            stream.Seek(sizeof(TextLayoutIconCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.ToggleBold"/> command.
        /// </summary>
        public void SeekPastToggleBoldCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.ToggleItalic"/> command.
        /// </summary>
        public void SeekPastToggleItalicCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.PushStyle"/> command.
        /// </summary>
        public void SeekPastPushStyleCommand()
        {
            stream.Seek(sizeof(TextLayoutStyleCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.PushFont"/> command.
        /// </summary>
        public void SeekPastPushFontCommand()
        {
            stream.Seek(sizeof(TextLayoutFontCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.PushColor"/> command.
        /// </summary>
        public void SeekPastPushColorCommand()
        {
            stream.Seek(sizeof(TextLayoutColorCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.PushGlyphShader"/> command.
        /// </summary>
        public void SeekPastPushGlyphShaderCommand()
        {
            stream.Seek(sizeof(TextLayoutGlyphShaderCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.PopStyle"/> command.
        /// </summary>
        public void SeekPastPopStyleCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.PopFont"/> command.
        /// </summary>
        public void SeekPastPopFontCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.PopColor"/> command.
        /// </summary>
        public void SeekPastPopColorCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.PopGlyphShader"/> command.
        /// </summary>
        public void SeekPastPopGlyphShaderCommand()
        {
            stream.Seek(sizeof(TextLayoutCommandType), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.ChangeSourceString"/> command.
        /// </summary>
        public void SeekPastChangeSourceStringCommand()
        {
            stream.Seek(sizeof(TextLayoutSourceStringCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.ChangeSourceStringBuilder"/> command.
        /// </summary>
        public void SeekPastChangeSourceStringBuilderCommand()
        {
            stream.Seek(sizeof(TextLayoutSourceStringBuilderCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Moves the stream past the current command, which is assume to be a <see cref="TextLayoutCommandType.Hyphen"/> command.
        /// </summary>
        public void SeekPastHyphenCommand()
        {
            stream.Seek(sizeof(TextLayoutSourceStringBuilderCommand), SeekOrigin.Current);
            streamPosition++;
        }

        /// <summary>
        /// Gets the layout settings which were used to produce the command stream.
        /// </summary>
        public TextLayoutSettings Settings
        {
            get;
            internal set;
        }
        
        /// <summary>
        /// Gets the parser options which were used to produce the command stream.
        /// </summary>
        public TextParserOptions ParserOptions
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the text that was processed by the layout engine.
        /// </summary>
        public StringSegment SourceText
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the bounds of the text after layout has been performed, relative to the layout area.
        /// </summary>
        public Rectangle Bounds
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the total width, in pixels, of the text after layout has been performed.
        /// </summary>
        public Int32 ActualWidth
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the total height, in pixels, of the text after layout has been performed.
        /// </summary>
        public Int32 ActualHeight
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the total length of the text which was laid out.
        /// </summary>
        public Int32 TotalLength
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the index of the command at the stream's current position.
        /// </summary>
        public Int32 StreamPosition
        {
            get { return streamPosition; }
        }

        /// <summary>
        /// Gets the number of commands in the stream.
        /// </summary>
        public Int32 Count
        {
            get { return stream.LengthInObjects; }
        }

        /// <summary>
        /// Gets the number of lines of text in the stream.
        /// </summary>
        public Int32 LineCount
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets a pointer to the stream's current position within its internal data buffer.
        /// </summary>
        public IntPtr Data
        {
            get
            {
                return stream.Data;
            }
        }

        /// <summary>
        /// Gets a pointer to the beginning of the stream's internal data buffer.
        /// </summary>
        public IntPtr Data0
        {
            get
            {
                return stream.Data0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the command stream has acquired pointers to its underlying buffers.
        /// </summary>
        public Boolean HasAcquiredPointers
        {
            get { return stream.HasAcquiredPointers; }
        }

        /// <summary>
        /// Registers a resource with the command stream.
        /// </summary>
        private Int32 RegisterResource<TResource>(StringSegment name, TResource resource, List<TResource> resourcesList, Dictionary<StringSegment, Int32> resourcesByName)
        {
            Int32 index;
            if (resourcesByName.TryGetValue(name, out index))
                return index;

            index = resourcesList.Count;

            resourcesList.Add(resource);
            resourcesByName[name] = index;

            return index;
        }

        /// <summary>
        /// Registers a source string or string builder with the command stream.
        /// </summary>
        private Int32 RegisterSource(Object source)
        {
            Int32 index;
            if (sourcesByReference.TryGetValue(source, out index))
                return index;

            sources.Add(source);
            sourcesByReference[source] = sources.Count - 1;

            return sources.Count - 1;
        }

        // The underlying data stream containing our commands.
        private readonly UnsafeObjectStream stream = new UnsafeObjectStream(32, 256);
        private Int32 streamPosition;

        // The stream's object registries.
        private readonly Dictionary<StringSegment, Int32> stylesByName = new Dictionary<StringSegment, Int32>();
        private readonly Dictionary<StringSegment, Int32> iconsByName = new Dictionary<StringSegment, Int32>();
        private readonly Dictionary<StringSegment, Int32> fontsByName = new Dictionary<StringSegment, Int32>();
        private readonly Dictionary<StringSegment, Int32> glyphShadersByName = new Dictionary<StringSegment, Int32>();
        private readonly Dictionary<Object, Int32> sourcesByReference = new Dictionary<Object, Int32>();
        private readonly List<TextStyle> styles = new List<TextStyle>();
        private readonly List<TextIconInfo> icons = new List<TextIconInfo>();
        private readonly List<SpriteFont> fonts = new List<SpriteFont>();
        private readonly List<GlyphShader> glyphShaders = new List<GlyphShader>();
        private readonly List<Object> sources = new List<Object>();
    }
}
