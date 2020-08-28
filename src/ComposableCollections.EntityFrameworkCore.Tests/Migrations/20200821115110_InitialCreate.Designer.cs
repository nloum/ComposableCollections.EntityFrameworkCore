﻿// <auto-generated />

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ComposableCollections.EntityFrameworkCore.Tests.Migrations
{
    [DbContext(typeof(MyDbContext))]
    [Migration("20200821115110_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.3");

            modelBuilder.Entity("LiveLinq.EntityFramework.Tests.PersonDto", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Person");
                });

            modelBuilder.Entity("LiveLinq.EntityFramework.Tests.WorkItemDto", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<Guid?>("AssignedToForeignKey")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("AssignedToForeignKey");

                    b.ToTable("WorkItem");
                });

            modelBuilder.Entity("LiveLinq.EntityFramework.Tests.WorkItemDto", b =>
                {
                    b.HasOne("LiveLinq.EntityFramework.Tests.PersonDto", "AssignedTo")
                        .WithMany("AssignedWorkItems")
                        .HasForeignKey("AssignedToForeignKey");
                });
#pragma warning restore 612, 618
        }
    }
}
