﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using mps.Model;

#nullable disable

namespace mps.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20231207150407_Recommendations")]
    partial class Recommendations
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Proxies:ChangeTracking", false)
                .HasAnnotation("Proxies:CheckEquality", false)
                .HasAnnotation("Proxies:LazyLoading", true);

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex");

                    b.ToTable("AspNetRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ClaimType")
                        .HasColumnType("TEXT");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("TEXT");

                    b.Property<string>("RoleId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ClaimType")
                        .HasColumnType("TEXT");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderKey")
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("RoleId")
                        .HasColumnType("TEXT");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("LoginProvider")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .HasColumnType("TEXT");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens", (string)null);
                });

            modelBuilder.Entity("mps.Model.ApplicationUser", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("TEXT");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("TEXT");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("TEXT");

                    b.Property<string>("ShortName")
                        .HasMaxLength(20)
                        .HasColumnType("TEXT");

                    b.Property<bool>("ShowShortnameInList")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UserName")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex");

                    b.ToTable("AspNetUsers", (string)null);

                    b.HasData(
                        new
                        {
                            Id = "00000000-0000-0000-0000-000000000000",
                            AccessFailedCount = 0,
                            ConcurrencyStamp = "2f35f45a-7e9d-40ed-9bd9-aad98fcb0fff",
                            EmailConfirmed = false,
                            LockoutEnabled = true,
                            PhoneNumberConfirmed = false,
                            SecurityStamp = "84591c13-42ea-4ad2-b26d-5590d1ca96b2",
                            ShowShortnameInList = false,
                            TwoFactorEnabled = false,
                            UserName = "SYSTEM"
                        });
                });

            modelBuilder.Entity("mps.Model.ItemHistory", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("Amount")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CheckDate")
                        .HasColumnType("TEXT");

                    b.Property<int>("ShoppingListItemId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("UnitId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ShoppingListItemId");

                    b.HasIndex("UnitId");

                    b.ToTable("ItemHistories");
                });

            modelBuilder.Entity("mps.Model.Recommendations", b =>
                {
                    b.Property<int>("ShoppingListId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ShoppingListItemId")
                        .HasColumnType("INTEGER");

                    b.HasIndex("ShoppingListId");

                    b.HasIndex("ShoppingListItemId");

                    b.ToTable((string)null);

                    b.ToView("Recommendations", (string)null);
                });

            modelBuilder.Entity("mps.Model.ShoppingList", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("OwnerUserKey")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("OwnerUserKey");

                    b.ToTable("ShoppingLists");
                });

            modelBuilder.Entity("mps.Model.ShoppingListItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("AddDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("AddedByUserId")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Amount")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("CheckDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("CheckedByUserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Hint")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("LastChangeByUserId")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastChangedDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<int>("ShoppingListId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ShortName")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("TEXT");

                    b.Property<int>("Status")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("UnitId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("AddedByUserId");

                    b.HasIndex("CheckedByUserId");

                    b.HasIndex("LastChangeByUserId");

                    b.HasIndex("ShoppingListId");

                    b.HasIndex("UnitId");

                    b.HasIndex("Name", "ShoppingListId")
                        .IsUnique();

                    b.ToTable("ShoppingListItems");
                });

            modelBuilder.Entity("mps.Model.Unit", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<string>("ShortName")
                        .IsRequired()
                        .HasMaxLength(5)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ShortName")
                        .IsUnique();

                    b.ToTable("Units");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Name = "Stück",
                            ShortName = "Stk"
                        },
                        new
                        {
                            Id = 2,
                            Name = "Gramm",
                            ShortName = "g"
                        },
                        new
                        {
                            Id = 3,
                            Name = "Kilo",
                            ShortName = "kg"
                        },
                        new
                        {
                            Id = 4,
                            Name = "Liter",
                            ShortName = "l"
                        });
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("mps.Model.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("mps.Model.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("mps.Model.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("mps.Model.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("mps.Model.ItemHistory", b =>
                {
                    b.HasOne("mps.Model.ShoppingListItem", "ShoppingListItem")
                        .WithMany("History")
                        .HasForeignKey("ShoppingListItemId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("mps.Model.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId");

                    b.Navigation("ShoppingListItem");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("mps.Model.Recommendations", b =>
                {
                    b.HasOne("mps.Model.ShoppingList", "ShoppingList")
                        .WithMany()
                        .HasForeignKey("ShoppingListId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("mps.Model.ShoppingListItem", "ShoppingListItem")
                        .WithMany()
                        .HasForeignKey("ShoppingListItemId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ShoppingList");

                    b.Navigation("ShoppingListItem");
                });

            modelBuilder.Entity("mps.Model.ShoppingList", b =>
                {
                    b.HasOne("mps.Model.ApplicationUser", "OwnerUser")
                        .WithMany()
                        .HasForeignKey("OwnerUserKey");

                    b.Navigation("OwnerUser");
                });

            modelBuilder.Entity("mps.Model.ShoppingListItem", b =>
                {
                    b.HasOne("mps.Model.ApplicationUser", "AddedByUser")
                        .WithMany()
                        .HasForeignKey("AddedByUserId");

                    b.HasOne("mps.Model.ApplicationUser", "CheckedByUser")
                        .WithMany()
                        .HasForeignKey("CheckedByUserId");

                    b.HasOne("mps.Model.ApplicationUser", "LastChangeByUser")
                        .WithMany()
                        .HasForeignKey("LastChangeByUserId");

                    b.HasOne("mps.Model.ShoppingList", "ShoppingList")
                        .WithMany("Items")
                        .HasForeignKey("ShoppingListId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("mps.Model.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId");

                    b.Navigation("AddedByUser");

                    b.Navigation("CheckedByUser");

                    b.Navigation("LastChangeByUser");

                    b.Navigation("ShoppingList");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("mps.Model.ShoppingList", b =>
                {
                    b.Navigation("Items");
                });

            modelBuilder.Entity("mps.Model.ShoppingListItem", b =>
                {
                    b.Navigation("History");
                });
#pragma warning restore 612, 618
        }
    }
}
