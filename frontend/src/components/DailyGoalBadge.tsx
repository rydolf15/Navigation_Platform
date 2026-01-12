interface DailyGoalBadgeProps {
  achieved: boolean;
}

export function DailyGoalBadge({ achieved }: DailyGoalBadgeProps) {
  if (!achieved) return null;

  return (
    <div
      style={{
        background: "#22c55e",
        color: "white",
        padding: "0.75rem 1rem",
        borderRadius: 8,
        marginBottom: "1rem",
        fontWeight: 600,
        textAlign: "center",
      }}
    >
        Daily distance goal achieved!
    </div>
  );
}
