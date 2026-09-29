using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels
{
    [QueryProperty(nameof(Post), "Post")]
    public partial class PostDetailsViewModel : ObservableObject
    {
        private readonly ApiService _api;
        private readonly DatabaseService _db;

        [ObservableProperty]
        private CommunityPost? _post;

        [ObservableProperty]
        private ObservableCollection<CommunityComment> _comments = new();

        [ObservableProperty]
        private string _newCommentText = string.Empty;

        [ObservableProperty]
        private bool _isRefreshing;

        // Reply state: tracks which comment is being replied to
        [ObservableProperty]
        private CommunityComment? _replyingToComment;

        [ObservableProperty]
        private string _replyingToName = string.Empty;

        [ObservableProperty]
        private bool _isReplying;

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        public PostDetailsViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
        }

        async partial void OnPostChanged(CommunityPost? value)
        {
            if (value != null)
            {
                await LoadCommentsAsync();
            }
        }

        [RelayCommand]
        public async Task LoadCommentsAsync()
        {
            if (Post == null) return;

            // Preserve old states
            var oldVisibleCounts = Comments
                .Where(c => c.TopLevelParent == c)
                .ToDictionary(c => c.Id, c => c.VisibleDescendantsCount);

            IsRefreshing = true;
            try
            {
                var data = await _api.GetCommentsAsync(Post.Id);
                
                var dict = data.ToDictionary(c => c.Id);
                int currentUserId = _db.CurrentUser?.Id ?? 0;
                bool isPostOwner = Post.UserId == currentUserId;
                foreach(var c in data) c.CanDelete = isPostOwner || c.UserId == currentUserId;
                var roots = new List<CommunityComment>();
                
                foreach (var c in data)
                {
                    if (c.ParentCommentId.HasValue && dict.TryGetValue(c.ParentCommentId.Value, out var parent))
                    {
                        parent.Replies.Add(c);
                    }
                    else
                    {
                        roots.Add(c);
                    }
                }
                
                Comments.Clear();
                foreach (var root in roots)
                {
                    root.TopLevelParent = root;
                    root.AllDescendants = new List<CommunityComment>();
                    CollectDescendants(root, root, 0);
                    
                    root.TotalDescendantsCount = root.AllDescendants.Count;
                    
                    if (oldVisibleCounts.TryGetValue(root.Id, out int oldVisible))
                    {
                        if (root.TotalDescendantsCount > oldVisible) {
                            root.VisibleDescendantsCount = root.TotalDescendantsCount;
                        } else {
                            root.VisibleDescendantsCount = Math.Min(oldVisible, root.TotalDescendantsCount);
                        }
                    }
                    else
                    {
                        root.VisibleDescendantsCount = 0;
                    }
                    
                    Comments.Add(root);
                    
                    for (int i = 0; i < root.VisibleDescendantsCount; i++)
                    {
                        Comments.Add(root.AllDescendants[i]);
                    }
                    
                    UpdatePaginatorForRoot(root);
                }
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        
        private void CollectDescendants(CommunityComment root, CommunityComment node, int depth)
        {
            node.Depth = depth;
            if (node != root)
            {
                node.TopLevelParent = root;
                root.AllDescendants.Add(node);
            }
            foreach (var child in node.Replies)
            {
                CollectDescendants(root, child, depth + 1);
            }
        }

        private void UpdatePaginatorForRoot(CommunityComment root)
        {
            root.IsPaginatorVisible = false;
            foreach (var d in root.AllDescendants)
            {
                d.IsPaginatorVisible = false;
                d.IsHideVisible = false;
            }
            
            int total = root.TotalDescendantsCount;
            if (total == 0) return;
            
            int visible = root.VisibleDescendantsCount;
            var anchor = visible == 0 ? root : root.AllDescendants[visible - 1];
            
            anchor.IsPaginatorVisible = true;
            anchor.IsHideVisible = visible > 0;
            
            int remaining = total - visible;
            if (remaining > 0)
            {
                if (visible == 0)
                    anchor.PaginatorText = $"View {total} replies \u2304";
                else
                    anchor.PaginatorText = $"View {Math.Min(5, remaining)} more \u2304";
            }
            else
            {
                anchor.PaginatorText = string.Empty;
            }
        }

        [RelayCommand]
        private void LoadMoreReplies(CommunityComment anchor)
        {
            var root = anchor.TopLevelParent ?? anchor;
            int currentVisible = root.VisibleDescendantsCount;
            int remaining = root.TotalDescendantsCount - currentVisible;
            int toAdd = Math.Min(5, remaining);
            
            if (toAdd <= 0) return;
            
            int anchorIndex = Comments.IndexOf(anchor);
            if (anchorIndex < 0) return;
            
            for (int i = 0; i < toAdd; i++)
            {
                var child = root.AllDescendants[currentVisible + i];
                Comments.Insert(anchorIndex + 1 + i, child);
            }
            
            root.VisibleDescendantsCount += toAdd;
            UpdatePaginatorForRoot(root);
        }

        [RelayCommand]
        private async Task DeleteCommentAsync(CommunityComment comment)
        {
            if (comment == null) return;
            bool confirm = await Shell.Current.DisplayAlert("Delete Comment", "Are you sure you want to delete this comment?", "Yes", "No");
            if (!confirm) return;

            var success = await _api.DeleteCommentAsync(comment.Id);
            if (success)
            {
                if (Post is not null)
                    Post.CommentsCount--;

                await LoadCommentsAsync();
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Failed to delete comment.", "OK");
            }
        }

        [RelayCommand]
        private void HideReplies(CommunityComment anchor)
        {
            var root = anchor.TopLevelParent ?? anchor;
            int visible = root.VisibleDescendantsCount;
            if (visible == 0) return;
            
            for (int i = 0; i < visible; i++)
            {
                Comments.Remove(root.AllDescendants[i]);
            }
            
            root.VisibleDescendantsCount = 0;
            UpdatePaginatorForRoot(root);
        }

        [RelayCommand]
        private async Task ToggleLikeAsync()
        {
            if (Post == null || _db.CurrentUser == null) return;

            Post.IsLikedByMe = !Post.IsLikedByMe;
            Post.LikesCount += Post.IsLikedByMe ? 1 : -1;

            int userId = _db.CurrentUser.Id;
            var newStatus = await _api.ToggleLikeAsync(Post.Id, userId);

            if (newStatus != Post.IsLikedByMe)
            {
                Post.IsLikedByMe = newStatus;
                Post.LikesCount += Post.IsLikedByMe ? 1 : -1;
            }
        }

        [RelayCommand]
        private async Task ToggleCommentLikeAsync(CommunityComment comment)
        {
            if (comment == null || _db.CurrentUser == null) return;

            // Optimistic update
            comment.IsLikedByMe = !comment.IsLikedByMe;
            comment.LikeCount += comment.IsLikedByMe ? 1 : -1;

            var newStatus = await _api.ToggleCommentLikeAsync(comment.Id, _db.CurrentUser.Id);

            if (newStatus != comment.IsLikedByMe)
            {
                comment.IsLikedByMe = newStatus;
                comment.LikeCount += comment.IsLikedByMe ? 1 : -1;
            }
        }

        [RelayCommand]
        private void StartReply(CommunityComment comment)
        {
            ReplyingToComment = comment;
            ReplyingToName = comment.AuthorName;
            IsReplying = true;
            NewCommentText = string.Empty;
        }

        [RelayCommand]
        private void CancelReply()
        {
            ReplyingToComment = null;
            ReplyingToName = string.Empty;
            IsReplying = false;
            NewCommentText = string.Empty;
        }

        [RelayCommand]
        private async Task SendCommentAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCommentText) || Post == null || _db.CurrentUser == null) return;

            int? parentId = ReplyingToComment?.Id;
            var success = await _api.AddCommentAsync(Post.Id, _db.CurrentUser.Id, NewCommentText, parentId);
            if (success)
            {
                NewCommentText = string.Empty;
                CancelReply();
                Post.CommentsCount++;
                await LoadCommentsAsync();
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Error", "Failed to add comment. Try again.", "OK");
            }
        }
    }
}











