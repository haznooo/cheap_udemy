
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System;
using System.Collections.Generic;

// Import Entity Framework Core classes such as:
// DbContext, DbSet, ModelBuilder, DeleteBehavior...etc
using Microsoft.EntityFrameworkCore;

// Import our entity classes:
// Course, Student, Instructor...etc

using DataAccess.Entities;
namespace DataAccess.Data
{
    public partial class AppDbContext : DbContext
    {

        public AppDbContext(DbContextOptions<AppDbContext> options): base(options)
        {
        }

        // Each DbSet<T> represents a table in the database
        public virtual DbSet<UserEntity> Users { get; set; }
        public virtual DbSet<UserProfileEntity> UsersProfile { get; set; }
        public virtual DbSet<RefreshTokenEntity> UserRefreshToken { get; set; }
        public virtual DbSet<AdminActionEntitiy> AdminActions { get; set; }
        public virtual DbSet<LoginLogEntitiy> LoginLogs { get; set; }
        public virtual DbSet<CourseEntitiy> Courses { get; set; }
        public virtual DbSet<SectionEntitiy> Sections { get; set; }
        public virtual DbSet<LessonEntity> Lessons { get; set; }
        public virtual DbSet<CategoryEntitiy> Categories { get; set; }
        public virtual DbSet<ReviewEntitiy> Reviews { get; set; }
        public virtual DbSet<EnrollmentEntitiy> Enrollments { get; set; }
        public virtual DbSet<PaymentEntitiy> Payments { get; set; }
        public virtual DbSet<UserLessonProgressEntitiy> UserLessonProgress { get; set; }



        // ModelBuilder describes how your database is supposed to look
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // mapping to the actual tables names in database (finished)
            modelBuilder.Entity<UserEntity>().ToTable("users");
            modelBuilder.Entity<UserProfileEntity>().ToTable("users_profile");
            modelBuilder.Entity<RefreshTokenEntity>().ToTable("user_refresh_tokens");
            modelBuilder.Entity<AdminActionEntitiy>().ToTable("admin_actions");
            modelBuilder.Entity<LoginLogEntitiy>().ToTable("login_logs");
            modelBuilder.Entity<CourseEntitiy>().ToTable("courses");
            modelBuilder.Entity<SectionEntitiy>().ToTable("sections");
            modelBuilder.Entity<LessonEntity>().ToTable("lessons");
            modelBuilder.Entity<CategoryEntitiy>().ToTable("categories");
            modelBuilder.Entity<ReviewEntitiy>().ToTable("reviews");
            modelBuilder.Entity<EnrollmentEntitiy>().ToTable("enrollments");
            modelBuilder.Entity<PaymentEntitiy>().ToTable("payments");
            modelBuilder.Entity<UserLessonProgressEntitiy>().ToTable("user_lesson_progress");
            base.OnModelCreating(modelBuilder);

            // Configure entities
            modelBuilder.Entity<UserEntity>(entity =>
            {
                // 1. Tell EF that user_id is the Primary Key
                entity.HasKey(e => e.user_id);
                entity.Property(e => e.user_id)
                      .ValueGeneratedOnAdd(); // Auto-incrementings

                entity.HasIndex(e => e.email, "ix_user_email")
                      .IsUnique();

                entity.HasIndex(e => e.username).IsUnique();
                entity.Property(e => e.username).HasMaxLength(20).IsRequired();

                entity.ToTable(b => b.HasCheckConstraint("valid_email_format", "email ~* '^.+@.+\\..+$'"));
                entity.ToTable(b => b.HasCheckConstraint("valid_username_format", "length(trim(username)) > 0"));    
                entity.Property(e => e.email).HasMaxLength(255).IsRequired();
                entity.Property(e => e.hashed_password).HasMaxLength(255);
                entity.Property(e => e.status).HasMaxLength(50);
                entity.Property(e => e.role).HasMaxLength(50);

                // Default creation date
                entity.Property(e => e.create_date)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone");
                // a user can has one profile
         

            });
            modelBuilder.Entity<UserProfileEntity>(entity =>
            {
                // Primary key = user_id
                entity.HasKey(e => e.user_id);

                // Not auto increment
                entity.Property(e => e.user_id)
                      .ValueGeneratedNever();

                entity.Property(e => e.image_url).HasColumnName("avatar_url").HasMaxLength(300);
                entity.Property(e => e.display_name).HasMaxLength(100);
                entity.Property(e => e.bio).HasMaxLength(500);

                // PERFECTED 1-to-1 CONFIGURATION:
                // This tells EF: "Profile has one User, and that User has one Profile"
                entity.HasOne(d => d.user)
                      .WithOne(u => u.UserProfile) // <-- This links the two entities together!
                      .HasForeignKey<UserProfileEntity>(d => d.user_id)
                      .HasConstraintName("fk_user_profile");
            });
   
            modelBuilder.Entity<SectionEntitiy>(entity =>
            {
                entity.HasKey(e => e.section_id);

                entity.Property(e => e.title).HasMaxLength(200).HasDefaultValue("Main").IsRequired();

                entity.Property(e => e.sort_order).IsRequired();

                // Ordering is unique within a course, not globally.
                entity.HasIndex(e => new { e.course_id, e.sort_order }, "uq_section_order_per_course")
                      .IsUnique();

                // FK to Course
                entity.HasOne(d => d.course)
                      .WithMany()
                      .HasForeignKey(d => d.course_id)
                      .HasConstraintName("fk_sections_courses").IsRequired();
            });
            modelBuilder.Entity<LessonEntity>(entity =>
            {
                entity.HasKey(e => e.lesson_id);

                entity.Property(e => e.title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.sort_order).HasDefaultValue(0).IsRequired();

                // Ordering is unique within a section.
                entity.HasIndex(e => new { e.section_id, e.sort_order }, "uq_lesson_order_per_section")
                      .IsUnique();

                // Configuration for content_blocks
                entity.Property(e => e.content_blocks)
                    .HasColumnType("jsonb")
                    .IsRequired()
                    .HasDefaultValueSql("'[]'::jsonb");

                // Configuration for lesson_metadata
                entity.Property(e => e.lesson_metadata)
                    .HasColumnType("jsonb")
                    .IsRequired()
                    .HasDefaultValueSql("'{\"video_duration\": 0, \"quiz_count\": 0, \"word_count\": 0}'::jsonb");

                // Default timestamps
                entity.Property(e => e.created_at)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.updated_at)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.estimated_duration_minutes).HasDefaultValue(0).IsRequired();

                // FK to Section
                entity.HasOne(d => d.section)
                      .WithMany()
                      .HasForeignKey(d => d.section_id)
                      .HasConstraintName("fk_lessons_sections");
            });


            modelBuilder.Entity<CourseEntitiy>()
                  .OwnsOne(e => e.course_metadata, builder =>
                  {
                      builder.ToJson();
                  });

            modelBuilder.Entity<CourseEntitiy>(entity =>
            {
                entity.HasKey(e => e.course_id);

                // Indexes
                entity.HasIndex(e => e.title, "ix_courses_title_fuzzy")
                      .HasMethod("GIN")
                      .HasOperators("gin_trgm_ops");
                entity.HasIndex(e => e.code, "idx_course");
                entity.HasIndex(e => e.instructor_id, "ix_courses_instructor_id");
                entity.HasIndex(e => e.category_id, "ix_courses_category_id");

                // Properties & Constraints
                entity.Property(e => e.title).HasMaxLength(150).IsRequired();
                entity.Property(e => e.code).HasMaxLength(30).IsRequired();

                entity.Property(e => e.price)
                      .HasPrecision(10, 2)
                      .HasDefaultValue(0.00m);

                entity.Property(e => e.status).HasMaxLength(50).HasDefaultValue("draft");
                entity.Property(e => e.level).HasMaxLength(50).HasDefaultValue("beginner");
                entity.Property(e => e.removal_reason).HasMaxLength(500);
                entity.Property(e => e.thumbnail_url).HasMaxLength(300);

                entity.Property(e => e.avg_rating)
                      .HasPrecision(3, 2)
                      .HasDefaultValue(0.00m);
                entity.Property(e => e.reviews_count)
                      .HasDefaultValue(0);

                entity.Property(e => e.created_date)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.published_date)
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.updated_at)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.deleted_at)
                      .HasColumnType("timestamp with time zone");

                // Relationships
                // Check Constraints
                entity.ToTable(t => {
                    t.HasCheckConstraint("CK_Courses_Price", "price >= 0");
                    t.HasCheckConstraint("CK_Courses_Duration", "estimated_duration_minutes >= 0");
                    t.HasCheckConstraint("ck_courses_rating", "avg_rating >= 0 AND avg_rating <= 5");
                    t.HasCheckConstraint("ck_courses_review_count", "reviews_count >= 0");
                });

                // Relationship to User (Instructor)
                entity.HasOne(d => d.instructor)
                      .WithMany()
                      .HasForeignKey(d => d.instructor_id)
                      .HasConstraintName("fk_courses_instructors");

                entity.HasOne(d => d.category)
                      .WithMany()
                      .HasForeignKey(d => d.category_id)
                      .HasConstraintName("fk_courses_categories");


            });

            modelBuilder.Entity<RefreshTokenEntity>(entity =>
            {
                entity.HasKey(e => e.token_id);
                entity.HasIndex(e => e.user_id, "idx_refresh_tokens_user");

                entity.Property(e => e.user_id).IsRequired();
                entity.Property(e => e.token_hash).HasMaxLength(255).IsRequired();
                entity.Property(e => e.device_info).HasMaxLength(255).IsRequired();

                entity.Property(e => e.expires_at).IsRequired();
                entity.Property(e => e.created_at).IsRequired().HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.revoked_at);

                entity.Property(e => e.is_used).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.chain_breached).IsRequired().HasDefaultValue(false); // Removed HasMaxLength(30) since it's a bool
                entity.Property(e => e.replaced_by_id).HasDefaultValue(null);
                entity.Property(e => e.last_used_at).HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.ip_address).HasMaxLength(45);

           
                entity.HasOne(d => d.user)                  // Token HAS ONE User
                     .WithMany(p => p.RefreshTokens)        // User HAS MANY RefreshTokens
                     .HasForeignKey(d => d.user_id)         // The Foreign Key pointing to User is user_id
                     .HasConstraintName("fk_user")
                     .OnDelete(DeleteBehavior.Cascade);    
            });


            modelBuilder.Entity<EnrollmentEntitiy>(entity =>
            {
                entity.HasKey(e => e.enrollment_id);

                entity.HasIndex(e => e.course_id, "ix_enrollments_course_id");
                entity.HasIndex(e => e.status, "ix_enrollments_status");
                entity.HasIndex(e => e.user_id, "ix_enrollments_user_id");

                // Prevent duplicate enrollment: Same user cannot enroll twice in the same course
                entity.HasIndex(e => new { e.user_id, e.course_id },
                                "uq_enrollments_user_course")
                      .IsUnique();

                entity.Property(e => e.completion_date)
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.enrollment_date)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone")
                      .IsRequired();

                // Decimal progress percentage
                entity.Property(e => e.progress_percentage)
                      .HasColumnType("decimal(5, 2)")
                      .HasDefaultValue(0.00);

                entity.Property(e => e.status)
                      .HasMaxLength(50)
                      .HasColumnName("status")
                      .HasDefaultValue("active");

                // Many enrollments belong to one course
                entity.HasOne(d => d.course)
                      .WithMany(p => p.enrollments)
                      .HasForeignKey(d => d.course_id)
                      .HasConstraintName("fk_enrollments_courses").OnDelete(DeleteBehavior.Restrict);

                // Many enrollments belong to one user
                entity.HasOne(d => d.user)
                      .WithMany()
                      .HasForeignKey(d => d.user_id)
                      .HasConstraintName("fk_enrollments_users").OnDelete(DeleteBehavior.Restrict);

                entity.ToTable(t => {
                    t.HasCheckConstraint("ck_enrollments_progress", "progress_percentage >= 0 AND progress_percentage <= 100");
                });
            });
   
            modelBuilder.Entity<AdminActionEntitiy>(entity =>
            {
                entity.HasKey(e => e.id);

                // Create index on admin_id
                entity.HasIndex(e => e.admin_id, "IX_AdminActions_AdminId");

                // Create index on performed_at
                entity.HasIndex(e => e.performed_at, "IX_AdminActions_PerformedAt");

                entity.Property(e => e.action_type).HasMaxLength(50).IsRequired();
                entity.Property(e => e.target_table).HasColumnName("target_table").HasMaxLength(50);

                // JSONB columns
                entity.Property(e => e.old_value)
                      .HasColumnType("jsonb");
                entity.Property(e => e.new_value)
                      .HasColumnType("jsonb");

                // Default timestamp
                entity.Property(e => e.performed_at)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone");

                entity.ToTable(t => {
                    t.HasCheckConstraint("valid_action_type", "action_type IN ('create', 'update', 'delete', 'ban', 'unban')");
                });

                // FK to User (Admin)
                entity.HasOne(d => d.admin)
                      .WithMany()
                      .HasForeignKey(d => d.admin_id)
                      .HasConstraintName("fk_admin_actions_users").IsRequired();
            });
            modelBuilder.Entity<LoginLogEntitiy>(entity =>
            {
                entity.HasKey(e => e.id);

                entity.HasIndex(e => e.attempted_at, "ix_login_logs_attempted_at");

                entity.Property(e => e.attempted_identifier).HasMaxLength(254);
                entity.Property(e => e.user_agent).HasMaxLength(500);
                entity.Property(e => e.status).HasMaxLength(50).IsRequired();

                // Default attempt timestamp
                entity.Property(e => e.attempted_at)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone").IsRequired();

                // FK to User (nullable: failed attempts for an unknown email are still logged)
                entity.HasOne(d => d.user)
                      .WithMany()
                      .HasForeignKey(d => d.user_id)
                      .HasConstraintName("fk_login_logs_users");
            });

            modelBuilder.Entity<CategoryEntitiy>(entity =>
            {
                entity.HasKey(e => e.category_id);

                entity.Property(e => e.name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.slug).HasMaxLength(100).IsRequired();

                entity.HasIndex(e => e.name)
                     .IsUnique();
                entity.HasIndex(e => e.slug)
                     .IsUnique();

                // Self-referencing FK to Parent Category
                entity.HasOne(d => d.parent)
                      .WithMany()
                      .HasForeignKey(d => d.parent_id)
                      .HasConstraintName("fk_category_parent");
            });
            modelBuilder.Entity<ReviewEntitiy>(entity =>
            {
                entity.HasKey(e => e.review_id);

                // Prevent duplicate reviews: same user cannot review same course twice
                entity.HasIndex(e => new { e.course_id, e.user_id },
                                "uq_user_course_review")
                      .IsUnique();

                // Default creation date
                entity.Property(e => e.created_at)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone");

                // Set on edit; null means the review has never been edited.
                entity.Property(e => e.updated_at)
                      .HasColumnType("timestamp with time zone");

                // Rating constraint: 1 to 5
                entity.Property(e => e.rating)
                      .HasDefaultValue((short)0);

                entity.Property(e => e.comment).HasMaxLength(1000);

                // FK to Course
                entity.HasOne(d => d.course)
                      .WithMany()
                      .HasForeignKey(d => d.course_id)
                      .HasConstraintName("fk_review_course");

                // FK to User
                entity.HasOne(d => d.user)
                      .WithMany()
                      .HasForeignKey(d => d.user_id)
                      .HasConstraintName("fk_review_user");
            });
            modelBuilder.Entity<PaymentEntitiy>(entity =>
            {
                entity.HasKey(e => e.payment_id);

                entity.HasIndex(e => e.user_id, "ix_payments_user_id");
                entity.HasIndex(e => e.course_id, "ix_payments_course_id");

                // Amount decimal with precision
                entity.Property(e => e.amount)
                      .HasColumnType("decimal(10, 2)")
                      .IsRequired();

                entity.Property(e => e.currency).HasMaxLength(3).HasDefaultValue("USD").IsRequired();
                entity.Property(e => e.status).HasMaxLength(20).HasDefaultValue("pending").IsRequired();
                entity.Property(e => e.provider).HasMaxLength(50).HasDefaultValue("simulated").IsRequired();
                entity.Property(e => e.provider_reference).HasMaxLength(100);

                // Default payment date
                entity.Property(e => e.payment_date)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone");

                entity.ToTable(t => {
                    t.HasCheckConstraint("ck_payments_amount", "amount >= 0");
                    t.HasCheckConstraint("valid_payment_status", "status IN ('pending', 'completed', 'failed', 'refunded')");
                });

                // FK to User
                entity.HasOne(d => d.user)
                      .WithMany()
                      .HasForeignKey(d => d.user_id)
                      .HasConstraintName("fk_payments_users");

                // FK to Course (what was bought)
                entity.HasOne(d => d.course)
                      .WithMany()
                      .HasForeignKey(d => d.course_id)
                      .HasConstraintName("fk_payments_courses");

                // FK to Enrollment (the enrollment it produced, if any)
                entity.HasOne(d => d.enrollment)
                      .WithMany()
                      .HasForeignKey(d => d.enrollment_id)
                      .HasConstraintName("fk_payments_enrollments")
                      .OnDelete(DeleteBehavior.SetNull);
            });
            modelBuilder.Entity<UserLessonProgressEntitiy>(entity =>
            {
                // Composite primary key
                entity.HasKey(e => new { e.user_id, e.lesson_id });

                // Default completion timestamp
                entity.Property(e => e.completed_at)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .HasColumnType("timestamp with time zone");

                // Default IsCompleted = false
                entity.Property(e => e.is_completed)
                      .HasDefaultValue(false);

                // FK to User
                entity.HasOne(d => d.user)
                      .WithMany()
                      .HasForeignKey(d => d.user_id)
                      .HasConstraintName("fk_user_lesson_progress_users");

                // FK to Lesson
                entity.HasOne(d => d.lesson)
                      .WithMany()
                      .HasForeignKey(d => d.lesson_id)
                      .HasConstraintName("fk_user_lesson_progress_lessons");
            });

            // if there are other configurations in another file, call the partial method to apply them
            OnModelCreatingPartial(modelBuilder);
        }

        // it allows to implemented more options for DbContext in another file
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
