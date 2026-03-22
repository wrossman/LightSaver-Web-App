import styles from "../styles/LinkContentContainer.module.css";
import type { ReactNode } from "react";

export function LinkContentContainer({ children }: { children: ReactNode }) {
    return (
        <div className={styles.linkContentContainer}>
            {children}
        </div>
    );
}
