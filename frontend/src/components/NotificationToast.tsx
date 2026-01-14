import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import "../styles/NotificationToast.css";

export interface Notification {
  id: string;
  type: "updated" | "deleted" | "shared";
  journeyId: string;
  journeyName?: string;
  timestamp: Date;
  read: boolean;
}

interface NotificationToastProps {
  notification: Notification;
  onDismiss: (id: string) => void;
  autoDismissMs?: number;
}

export function NotificationToast({
  notification,
  onDismiss,
  autoDismissMs = 5000,
}: NotificationToastProps) {
  const navigate = useNavigate();
  const [isVisible, setIsVisible] = useState(true);

  useEffect(() => {
    if (autoDismissMs > 0) {
      const timer = setTimeout(() => {
        setIsVisible(false);
        setTimeout(() => onDismiss(notification.id), 300); // Wait for fade out
      }, autoDismissMs);

      return () => clearTimeout(timer);
    }
  }, [autoDismissMs, notification.id, onDismiss]);

  const handleClick = () => {
    if (notification.type !== "deleted") {
      navigate(`/journeys/${notification.journeyId}`);
    }
    onDismiss(notification.id);
  };

  const getMessage = () => {
    const journeyName = notification.journeyName || "A journey";
    switch (notification.type) {
      case "updated":
        return `${journeyName} was updated`;
      case "deleted":
        return `${journeyName} was deleted`;
      case "shared":
        return `${journeyName} was shared with you`;
      default:
        return "New notification";
    }
  };

  const getIcon = () => {
    switch (notification.type) {
      case "updated":
        return "✏️";
      case "deleted":
        return "🗑️";
      case "shared":
        return "🔗";
      default:
        return "ℹ️";
    }
  };

  if (!isVisible) return null;

  return (
    <div
      className={`notification-toast notification-toast--${notification.type} ${
        !notification.read ? "notification-toast--unread" : ""
      }`}
      onClick={handleClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          handleClick();
        }
      }}
    >
      <div className="notification-toast__icon">{getIcon()}</div>
      <div className="notification-toast__content">
        <p className="notification-toast__message">{getMessage()}</p>
        <span className="notification-toast__time">
          {formatTime(notification.timestamp)}
        </span>
      </div>
      <button
        className="notification-toast__dismiss"
        onClick={(e) => {
          e.stopPropagation();
          onDismiss(notification.id);
        }}
        aria-label="Dismiss notification"
      >
        ×
      </button>
    </div>
  );
}

function formatTime(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSecs = Math.floor(diffMs / 1000);
  const diffMins = Math.floor(diffSecs / 60);
  const diffHours = Math.floor(diffMins / 60);

  if (diffSecs < 60) return "Just now";
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  return date.toLocaleDateString();
}
