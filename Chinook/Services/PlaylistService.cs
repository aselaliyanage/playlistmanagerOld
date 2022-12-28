﻿using Chinook.ClientModels;
using Chinook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace Chinook.Services
{
    public class PlaylistService : IPlaylistService
    {
        private readonly IDbContextFactory<ChinookContext> _appDbContext;

        public PlaylistService(IDbContextFactory<ChinookContext> appDbContextFactory)
        {
            _appDbContext = appDbContextFactory;
        }

        public async Task<List<ClientModels.Playlist>> GetCurrentUserPlaylistsAsync()
        {
            using var dbContext = await _appDbContext.CreateDbContextAsync();
            
            return dbContext.UserPlaylists
                .Include(x => x.Playlist)
                .Select(x => new ClientModels.Playlist
                {
                    Id = x.PlaylistId,
                    Name = x.Playlist.Name!
                }).ToList();
        }

        public async Task<ClientModels.Playlist> GetPlayListAsyn(string currentUserId, long playListId)
        {
            using var dbContext = await _appDbContext.CreateDbContextAsync();
            var playlist = dbContext.Playlists
            .Include(a => a.Tracks).ThenInclude(a => a.Album).ThenInclude(a => a.Artist)
            .Where(p => p.PlaylistId == playListId)
            .Select(p => new ClientModels.Playlist()
            {
                Name = p.Name,
                Tracks = p.Tracks.Select(t => new ClientModels.PlaylistTrack()
                {
                    AlbumTitle = t.Album.Title,
                    ArtistName = t.Album.Artist.Name,
                    TrackId = t.TrackId,
                    TrackName = t.Name,
                    IsFavorite = t.Playlists.Where(p => p.UserPlaylists.Any(up => up.UserId == currentUserId && up.Playlist.Name == "Favorites")).Any()
                }).ToList()
            })
            .FirstOrDefault();

            return playlist;
        }        

        public async Task<bool> CreateNewPlaylistAsync(Models.Playlist playlist, string userId)
        {
            try
            {
                using var dbContext = await _appDbContext.CreateDbContextAsync();
                await CreateNewPlaylistAsync(playlist, userId, dbContext);
                await dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private async Task<bool> CreateNewPlaylistAsync(Models.Playlist playlist, string userId, ChinookContext dbContext)
        {
            await CreatePlaylistAsync(playlist, dbContext);
            await dbContext.UserPlaylists.AddAsync(new UserPlaylist() { PlaylistId = playlist.PlaylistId, UserId = userId });
            return true;
        }
        private static async Task<long> CreatePlaylistAsync(Models.Playlist playlist, ChinookContext dbContext)
        {
            var maxId = dbContext.Playlists.OrderByDescending(x => x.PlaylistId).FirstOrDefault()?.PlaylistId;
            playlist.PlaylistId = maxId == null ? 1 : (long)++maxId;

            await dbContext.Playlists.AddAsync(playlist);
            return playlist.PlaylistId;
        }

        public async Task<bool> AddTrackToPlaylistAsync(string newPlaylistName, long playlistId, long trackId, string userId)
        {
            var isNewPlaylist = !string.IsNullOrEmpty(newPlaylistName);
            var playList = new Models.Playlist() { Name = newPlaylistName,  };

            try
            {
                using var dbContext = await _appDbContext.CreateDbContextAsync();
                var playListInDb = dbContext.Playlists.Include(x => x.Tracks).AsTracking().Where(x => x.PlaylistId == playlistId).FirstOrDefault();
                var track = dbContext.Tracks.Where(x => x.TrackId == trackId).FirstOrDefault();

                if (isNewPlaylist)
                {
                    var newPlayList = new Models.Playlist()
                    {
                        Name = newPlaylistName, 
                        Tracks = new List<Track>() { track },
                    };
                    await CreateNewPlaylistAsync(newPlayList, userId, dbContext);
                }
                else
                {
                    if (playListInDb?.Tracks != null)
                    {
                        playListInDb.Tracks.Add(track);
                    }
                    else
                    {
                        playListInDb!.Tracks = new List<Track> { track };
                    }
                }

                dbContext.SaveChanges();
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }
    }


}
