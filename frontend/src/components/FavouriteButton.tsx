interface FavouriteButtonProps {
  isFavourite: boolean;
  onToggle(): void;
  disabled?: boolean;
}

export function FavouriteButton({
  isFavourite,
  onToggle,
  disabled,
}: FavouriteButtonProps) {
  return (
    <button
      type="button"
      onClick={onToggle}
      disabled={disabled}
      aria-pressed={isFavourite}
      aria-label={isFavourite ? "Unfavourite journey" : "Favourite journey"}
      style={{
        padding: "0.4rem 0.6rem",
        borderRadius: 4,
        border: "1px solid #e5e7eb",
        background: isFavourite ? "#fde68a" : "white",
        cursor: "pointer",
      }}
    >
      {isFavourite ? "★ Favourited" : "☆ Favourite"}
    </button>
  );
}
