import styles from "../styles/LinkContainer.module.css";
import type { ReactNode } from "react";

export function LinkContainer({ children }: { children: ReactNode }) {
    return (
        <div className={styles.linkContainer}>
            {children}
        </div>
    );
}
