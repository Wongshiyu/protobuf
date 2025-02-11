#region Copyright notice and license
// Protocol Buffers - Google's data interchange format
// Copyright 2008 Google Inc.  All rights reserved.
// https://developers.google.com/protocol-buffers/
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//
//     * Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above
// copyright notice, this list of conditions and the following disclaimer
// in the documentation and/or other materials provided with the
// distribution.
//     * Neither the name of Google Inc. nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

using Google.Protobuf.TestProtos;
using NUnit.Framework;
using ProtobufUnittest;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitTest.Issues.TestProtos;

namespace Google.Protobuf.Reflection
{
    /// <summary>
    /// Tests for descriptors. (Not in its own namespace or broken up into individual classes as the
    /// size doesn't warrant it. On the other hand, this makes me feel a bit dirty...)
    /// </summary>
    public class DescriptorsTest
    {
        [Test]
        public void FileDescriptor_GeneratedCode()
        {
            TestFileDescriptor(
                UnittestProto3Reflection.Descriptor,
                UnittestImportProto3Reflection.Descriptor,
                UnittestImportPublicProto3Reflection.Descriptor);
        }

        [Test]
        public void FileDescriptor_BuildFromByteStrings()
        {
            // The descriptors have to be supplied in an order such that all the
            // dependencies come before the descriptors depending on them.
            var descriptorData = new List<ByteString>
            {
                UnittestImportPublicProto3Reflection.Descriptor.SerializedData,
                UnittestImportProto3Reflection.Descriptor.SerializedData,
                UnittestProto3Reflection.Descriptor.SerializedData
            };
            var converted = FileDescriptor.BuildFromByteStrings(descriptorData);
            Assert.AreEqual(3, converted.Count);
            TestFileDescriptor(converted[2], converted[1], converted[0]);
        }

        [Test]
        public void FileDescriptor_BuildFromByteStrings_WithExtensionRegistry()
        {
            var extension = UnittestCustomOptionsProto3Extensions.MessageOpt1;

            var byteStrings = new[]
            {
                DescriptorReflection.Descriptor.Proto.ToByteString(),
                UnittestCustomOptionsProto3Reflection.Descriptor.Proto.ToByteString()
            };
            var registry = new ExtensionRegistry { extension };

            var descriptor = FileDescriptor.BuildFromByteStrings(byteStrings, registry).Last();
            var message = descriptor.MessageTypes.Single(t => t.Name == nameof(TestMessageWithCustomOptions));
            var extensionValue = message.GetOptions().GetExtension(extension);
            Assert.AreEqual(-56, extensionValue);
        }

        private void TestFileDescriptor(FileDescriptor file, FileDescriptor importedFile, FileDescriptor importedPublicFile)
        {
            Assert.AreEqual("csharp/protos/unittest_proto3.proto", file.Name);
            Assert.AreEqual("protobuf_unittest3", file.Package);

            Assert.AreEqual("UnittestProto", file.Proto.Options.JavaOuterClassname);
            Assert.AreEqual("csharp/protos/unittest_proto3.proto", file.Proto.Name);

            // unittest_proto3.proto doesn't have any public imports, but unittest_import_proto3.proto does.
            Assert.AreEqual(0, file.PublicDependencies.Count);
            Assert.AreEqual(1, importedFile.PublicDependencies.Count);
            Assert.AreEqual(importedPublicFile, importedFile.PublicDependencies[0]);

            Assert.AreEqual(1, file.Dependencies.Count);
            Assert.AreEqual(importedFile, file.Dependencies[0]);

            Assert.Null(file.FindTypeByName<MessageDescriptor>("NoSuchType"));
            Assert.Null(file.FindTypeByName<MessageDescriptor>("protobuf_unittest3.TestAllTypes"));
            for (int i = 0; i < file.MessageTypes.Count; i++)
            {
                Assert.AreEqual(i, file.MessageTypes[i].Index);
            }

            Assert.AreEqual(file.EnumTypes[0], file.FindTypeByName<EnumDescriptor>("ForeignEnum"));
            Assert.Null(file.FindTypeByName<EnumDescriptor>("NoSuchType"));
            Assert.Null(file.FindTypeByName<EnumDescriptor>("protobuf_unittest3.ForeignEnum"));
            Assert.AreEqual(1, importedFile.EnumTypes.Count);
            Assert.AreEqual("ImportEnum", importedFile.EnumTypes[0].Name);
            for (int i = 0; i < file.EnumTypes.Count; i++)
            {
                Assert.AreEqual(i, file.EnumTypes[i].Index);
            }

            Assert.AreEqual(10, file.SerializedData[0]);
            TestDescriptorToProto(file.ToProto, file.Proto);
        }

        [Test]
        public void FileDescriptor_BuildFromByteStrings_MissingDependency()
        {
            var descriptorData = new List<ByteString>
            {
                UnittestImportProto3Reflection.Descriptor.SerializedData,
                UnittestProto3Reflection.Descriptor.SerializedData,
            };
            // This will fail, because we're missing UnittestImportPublicProto3Reflection
            Assert.Throws<ArgumentException>(() => FileDescriptor.BuildFromByteStrings(descriptorData));
        }

        [Test]
        public void FileDescriptor_BuildFromByteStrings_DuplicateNames()
        {
            var descriptorData = new List<ByteString>
            {
                UnittestImportPublicProto3Reflection.Descriptor.SerializedData,
                UnittestImportPublicProto3Reflection.Descriptor.SerializedData,
            };
            // This will fail due to the same name being used twice
            Assert.Throws<ArgumentException>(() => FileDescriptor.BuildFromByteStrings(descriptorData));
        }

        [Test]
        public void FileDescriptor_BuildFromByteStrings_IncorrectOrder()
        {
            var descriptorData = new List<ByteString>
            {
                UnittestProto3Reflection.Descriptor.SerializedData,
                UnittestImportPublicProto3Reflection.Descriptor.SerializedData,
                UnittestImportProto3Reflection.Descriptor.SerializedData
            };
            // This will fail, because the dependencies should come first
            Assert.Throws<ArgumentException>(() => FileDescriptor.BuildFromByteStrings(descriptorData));

        }

        [Test]
        public void MessageDescriptorFromGeneratedCodeFileDescriptor()
        {
            var file = UnittestProto3Reflection.Descriptor;

            MessageDescriptor messageType = TestAllTypes.Descriptor;
            Assert.AreSame(typeof(TestAllTypes), messageType.ClrType);
            Assert.AreSame(TestAllTypes.Parser, messageType.Parser);
            Assert.AreEqual(messageType, file.MessageTypes[0]);
            Assert.AreEqual(messageType, file.FindTypeByName<MessageDescriptor>("TestAllTypes"));
        }

        [Test]
        public void MessageDescriptor()
        {
            MessageDescriptor messageType = TestAllTypes.Descriptor;
            MessageDescriptor nestedType = TestAllTypes.Types.NestedMessage.Descriptor;

            Assert.AreEqual("TestAllTypes", messageType.Name);
            Assert.AreEqual("protobuf_unittest3.TestAllTypes", messageType.FullName);
            Assert.AreEqual(UnittestProto3Reflection.Descriptor, messageType.File);
            Assert.IsNull(messageType.ContainingType);
            Assert.IsNull(messageType.Proto.Options);

            Assert.AreEqual("TestAllTypes", messageType.Name);

            Assert.AreEqual("NestedMessage", nestedType.Name);
            Assert.AreEqual("protobuf_unittest3.TestAllTypes.NestedMessage", nestedType.FullName);
            Assert.AreEqual(UnittestProto3Reflection.Descriptor, nestedType.File);
            Assert.AreEqual(messageType, nestedType.ContainingType);

            FieldDescriptor field = messageType.Fields.InDeclarationOrder()[0];
            Assert.AreEqual("single_int32", field.Name);
            Assert.AreEqual(field, messageType.FindDescriptor<FieldDescriptor>("single_int32"));
            Assert.Null(messageType.FindDescriptor<FieldDescriptor>("no_such_field"));
            Assert.AreEqual(field, messageType.FindFieldByNumber(1));
            Assert.Null(messageType.FindFieldByNumber(571283));
            var fieldsInDeclarationOrder = messageType.Fields.InDeclarationOrder();
            for (int i = 0; i < fieldsInDeclarationOrder.Count; i++)
            {
                Assert.AreEqual(i, fieldsInDeclarationOrder[i].Index);
            }

            Assert.AreEqual(nestedType, messageType.NestedTypes[0]);
            Assert.AreEqual(nestedType, messageType.FindDescriptor<MessageDescriptor>("NestedMessage"));
            Assert.Null(messageType.FindDescriptor<MessageDescriptor>("NoSuchType"));
            for (int i = 0; i < messageType.NestedTypes.Count; i++)
            {
                Assert.AreEqual(i, messageType.NestedTypes[i].Index);
            }

            Assert.AreEqual(messageType.EnumTypes[0], messageType.FindDescriptor<EnumDescriptor>("NestedEnum"));
            Assert.Null(messageType.FindDescriptor<EnumDescriptor>("NoSuchType"));
            for (int i = 0; i < messageType.EnumTypes.Count; i++)
            {
                Assert.AreEqual(i, messageType.EnumTypes[i].Index);
            }
            TestDescriptorToProto(messageType.ToProto, messageType.Proto);
        }

        [Test]
        public void FieldDescriptor_GeneratedCode()
        {
            TestFieldDescriptor(UnittestProto3Reflection.Descriptor, TestAllTypes.Descriptor, ForeignMessage.Descriptor, ImportMessage.Descriptor);
        }

        [Test]
        public void FieldDescriptor_BuildFromByteStrings()
        {
            // The descriptors have to be supplied in an order such that all the
            // dependencies come before the descriptors depending on them.
            var descriptorData = new List<ByteString>
            {
                UnittestImportPublicProto3Reflection.Descriptor.SerializedData,
                UnittestImportProto3Reflection.Descriptor.SerializedData,
                UnittestProto3Reflection.Descriptor.SerializedData
            };
            var converted = FileDescriptor.BuildFromByteStrings(descriptorData);
            TestFieldDescriptor(
                converted[2],
                converted[2].FindTypeByName<MessageDescriptor>("TestAllTypes"),
                converted[2].FindTypeByName<MessageDescriptor>("ForeignMessage"),
                converted[1].FindTypeByName<MessageDescriptor>("ImportMessage"));
        }

        public void TestFieldDescriptor(
            FileDescriptor unitTestProto3Descriptor,
            MessageDescriptor testAllTypesDescriptor,
            MessageDescriptor foreignMessageDescriptor,
            MessageDescriptor importMessageDescriptor)
        {
            FieldDescriptor primitiveField = testAllTypesDescriptor.FindDescriptor<FieldDescriptor>("single_int32");
            FieldDescriptor enumField = testAllTypesDescriptor.FindDescriptor<FieldDescriptor>("single_nested_enum");
            FieldDescriptor foreignMessageField = testAllTypesDescriptor.FindDescriptor<FieldDescriptor>("single_foreign_message");
            FieldDescriptor importMessageField = testAllTypesDescriptor.FindDescriptor<FieldDescriptor>("single_import_message");
            FieldDescriptor fieldInOneof = testAllTypesDescriptor.FindDescriptor<FieldDescriptor>("oneof_string");

            Assert.AreEqual("single_int32", primitiveField.Name);
            Assert.AreEqual("protobuf_unittest3.TestAllTypes.single_int32",
                            primitiveField.FullName);
            Assert.AreEqual(1, primitiveField.FieldNumber);
            Assert.AreEqual(testAllTypesDescriptor, primitiveField.ContainingType);
            Assert.AreEqual(unitTestProto3Descriptor, primitiveField.File);
            Assert.AreEqual(FieldType.Int32, primitiveField.FieldType);
            Assert.IsNull(primitiveField.Proto.Options);

            Assert.AreEqual("single_nested_enum", enumField.Name);
            Assert.AreEqual(FieldType.Enum, enumField.FieldType);
            Assert.AreEqual(testAllTypesDescriptor.EnumTypes[0], enumField.EnumType);

            Assert.AreEqual("single_foreign_message", foreignMessageField.Name);
            Assert.AreEqual(FieldType.Message, foreignMessageField.FieldType);
            Assert.AreEqual(foreignMessageDescriptor, foreignMessageField.MessageType);

            Assert.AreEqual("single_import_message", importMessageField.Name);
            Assert.AreEqual(FieldType.Message, importMessageField.FieldType);
            Assert.AreEqual(importMessageDescriptor, importMessageField.MessageType);

            // For a field in a regular onoef, ContainingOneof and RealContainingOneof should be the same.
            Assert.AreEqual("oneof_field", fieldInOneof.ContainingOneof.Name);
            Assert.AreSame(fieldInOneof.ContainingOneof, fieldInOneof.RealContainingOneof);

            TestDescriptorToProto(primitiveField.ToProto, primitiveField.Proto);
            TestDescriptorToProto(enumField.ToProto, enumField.Proto);
            TestDescriptorToProto(foreignMessageField.ToProto, foreignMessageField.Proto);
            TestDescriptorToProto(fieldInOneof.ToProto, fieldInOneof.Proto);
        }

        [Test]
        public void FieldDescriptorLabel()
        {
            FieldDescriptor singleField =
                TestAllTypes.Descriptor.FindDescriptor<FieldDescriptor>("single_int32");
            FieldDescriptor repeatedField =
                TestAllTypes.Descriptor.FindDescriptor<FieldDescriptor>("repeated_int32");

            Assert.IsFalse(singleField.IsRepeated);
            Assert.IsTrue(repeatedField.IsRepeated);
        }

        [Test]
        public void EnumDescriptor()
        {
            // Note: this test is a bit different to the Java version because there's no static way of getting to the descriptor
            EnumDescriptor enumType = UnittestProto3Reflection.Descriptor.FindTypeByName<EnumDescriptor>("ForeignEnum");
            EnumDescriptor nestedType = TestAllTypes.Descriptor.FindDescriptor<EnumDescriptor>("NestedEnum");

            Assert.AreEqual("ForeignEnum", enumType.Name);
            Assert.AreEqual("protobuf_unittest3.ForeignEnum", enumType.FullName);
            Assert.AreEqual(UnittestProto3Reflection.Descriptor, enumType.File);
            Assert.Null(enumType.ContainingType);
            Assert.Null(enumType.Proto.Options);

            Assert.AreEqual("NestedEnum", nestedType.Name);
            Assert.AreEqual("protobuf_unittest3.TestAllTypes.NestedEnum",
                            nestedType.FullName);
            Assert.AreEqual(UnittestProto3Reflection.Descriptor, nestedType.File);
            Assert.AreEqual(TestAllTypes.Descriptor, nestedType.ContainingType);

            EnumValueDescriptor value = enumType.FindValueByName("FOREIGN_FOO");
            Assert.AreEqual(value, enumType.Values[1]);
            Assert.AreEqual("FOREIGN_FOO", value.Name);
            Assert.AreEqual(4, value.Number);
            Assert.AreEqual((int) ForeignEnum.ForeignFoo, value.Number);
            Assert.AreEqual(value, enumType.FindValueByNumber(4));
            Assert.Null(enumType.FindValueByName("NO_SUCH_VALUE"));
            for (int i = 0; i < enumType.Values.Count; i++)
            {
                Assert.AreEqual(i, enumType.Values[i].Index);
            }
            TestDescriptorToProto(enumType.ToProto, enumType.Proto);
            TestDescriptorToProto(nestedType.ToProto, nestedType.Proto);
        }

        [Test]
        public void OneofDescriptor()
        {
            OneofDescriptor descriptor = TestAllTypes.Descriptor.FindDescriptor<OneofDescriptor>("oneof_field");
            Assert.IsFalse(descriptor.IsSynthetic);
            Assert.AreEqual("oneof_field", descriptor.Name);
            Assert.AreEqual("protobuf_unittest3.TestAllTypes.oneof_field", descriptor.FullName);

            var expectedFields = new[] {
                TestAllTypes.OneofBytesFieldNumber,
                TestAllTypes.OneofNestedMessageFieldNumber,
                TestAllTypes.OneofStringFieldNumber,
                TestAllTypes.OneofUint32FieldNumber }
                .Select(fieldNumber => TestAllTypes.Descriptor.FindFieldByNumber(fieldNumber))
                .ToList();
            foreach (var field in expectedFields)
            {
                Assert.AreSame(descriptor, field.ContainingOneof);
            }

            CollectionAssert.AreEquivalent(expectedFields, descriptor.Fields);
            TestDescriptorToProto(descriptor.ToProto, descriptor.Proto);
        }

        [Test]
        public void MapEntryMessageDescriptor()
        {
            var descriptor = MapWellKnownTypes.Descriptor.NestedTypes[0];
            Assert.IsNull(descriptor.Parser);
            Assert.IsNull(descriptor.ClrType);
            Assert.IsNull(descriptor.Fields[1].Accessor);
            TestDescriptorToProto(descriptor.ToProto, descriptor.Proto);
        }

        // From TestFieldOrdering:
        // string my_string = 11;
        // int64 my_int = 1;
        // float my_float = 101;
        // NestedMessage single_nested_message = 200;
        [Test]
        public void FieldListOrderings()
        {
            var fields = TestFieldOrderings.Descriptor.Fields;
            Assert.AreEqual(new[] { 11, 1, 101, 200 }, fields.InDeclarationOrder().Select(x => x.FieldNumber));
            Assert.AreEqual(new[] { 1, 11, 101, 200 }, fields.InFieldNumberOrder().Select(x => x.FieldNumber));
        }


        [Test]
        public void DescriptorProtoFileDescriptor()
        {
            var descriptor = Google.Protobuf.Reflection.FileDescriptor.DescriptorProtoFileDescriptor;
            Assert.AreEqual("google/protobuf/descriptor.proto", descriptor.Name);
            TestDescriptorToProto(descriptor.ToProto, descriptor.Proto);
        }

        [Test]
        public void DescriptorImportingExtensionsFromOldCodeGen()
        {
            // The extension collection includes a null extension. There's not a lot we can do about that
            // in itself, as the old generator didn't provide us the extension information.
            var extensions = TestProtos.OldGenerator.OldExtensions2Reflection.Descriptor.Extensions;
            Assert.AreEqual(1, extensions.UnorderedExtensions.Count);
            // Note: this assertion is present so that it will fail if OldExtensions2 is regenerated
            // with a new generator.
            Assert.Null(extensions.UnorderedExtensions[0].Extension);

            // ... but we can make sure we at least don't cause a failure when retrieving descriptors.
            // In particular, old_extensions1.proto imports old_extensions2.proto, and this used to cause
            // an execution-time failure.
            var importingDescriptor = TestProtos.OldGenerator.OldExtensions1Reflection.Descriptor;
            Assert.NotNull(importingDescriptor);
        }

        [Test]
        public void Proto3OptionalDescriptors()
        {
            var descriptor = TestProto3Optional.Descriptor;
            var field = descriptor.Fields[TestProto3Optional.OptionalInt32FieldNumber];
            Assert.NotNull(field.ContainingOneof);
            Assert.IsTrue(field.ContainingOneof.IsSynthetic);
            Assert.Null(field.RealContainingOneof);
        }


        [Test]
        public void SyntheticOneofReflection()
        {
            // Expect every oneof in TestProto3Optional to be synthetic
            var proto3OptionalDescriptor = TestProto3Optional.Descriptor;
            Assert.AreEqual(0, proto3OptionalDescriptor.RealOneofCount);
            foreach (var oneof in proto3OptionalDescriptor.Oneofs)
            {
                Assert.True(oneof.IsSynthetic);
            }

            // Expect no oneof in the original proto3 unit test file to be synthetic.
            // (This excludes oneofs with "lazy" in the name, due to internal differences.)
            foreach (var descriptor in ProtobufTestMessages.Proto3.TestMessagesProto3Reflection.Descriptor.MessageTypes)
            {
                var nonLazyOneofs = descriptor.Oneofs.Where(d => !d.Name.Contains("lazy")).ToList();
                Assert.AreEqual(nonLazyOneofs.Count, descriptor.RealOneofCount);
                foreach (var oneof in nonLazyOneofs)
                {
                    Assert.False(oneof.IsSynthetic);
                }
            }

            // Expect no oneof in the original proto2 unit test file to be synthetic.
            foreach (var descriptor in ProtobufTestMessages.Proto2.TestMessagesProto2Reflection.Descriptor.MessageTypes)
            {
                Assert.AreEqual(descriptor.Oneofs.Count, descriptor.RealOneofCount);
                foreach (var oneof in descriptor.Oneofs)
                {
                    Assert.False(oneof.IsSynthetic);
                }
            }
        }

        private static void TestDescriptorToProto(Func<IMessage> toProtoFunction, IMessage expectedProto)
        {
            var clone1 = toProtoFunction();
            var clone2 = toProtoFunction();
            Assert.AreNotSame(clone1, clone2);
            Assert.AreNotSame(clone1, expectedProto);
            Assert.AreNotSame(clone2, expectedProto);

            Assert.AreEqual(clone1, clone2);
            Assert.AreEqual(clone1, expectedProto);
        }
    }
}
