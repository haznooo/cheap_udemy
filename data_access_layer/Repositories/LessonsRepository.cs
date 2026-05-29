using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Repositories
{
    public class LessonsRepository(AppDbContext context)
    {

        public async Task<LessonEntity> AddLessonAsync(LessonEntity lesson)
        {
            context.Lessons.Add(lesson);
            await context.SaveChangesAsync();
            return lesson;
        }

        public async Task<LessonEntity?> GetLessonByIdAsync(int lessonId)
        {
            // AsNoTracking is great for read-only operations to boost performance
            return await context.Lessons
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.lesson_id == lessonId);
        }


    }
    }

