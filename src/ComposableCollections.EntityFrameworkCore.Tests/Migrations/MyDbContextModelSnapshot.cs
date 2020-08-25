﻿// <auto-generated />

using System;

namespace ComposableCollections.EntityFrameworkCore.Tests.Migrations
{
    [DbContext(typeof(MyDbContext))]
    partial class MyDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
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